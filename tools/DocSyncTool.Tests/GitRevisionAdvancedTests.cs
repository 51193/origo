using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace DocSyncTool.Tests;

/// <summary>
///     Additional git-history revision-planner coverage for less common but
///     real workflows: new translations joining an existing pair, leading-side
///     drift, unrelated-pair isolation, metadata injection, v1 snapshot
///     migration shapes, and the git plumbing helpers used by the planner.
/// </summary>
[Collection("DocSyncToolConsoleCapture")]
public class GitRevisionAdvancedTests
{
    private static void RunGenerator(TestRepo repo) =>
        ConsoleOutputCapture.Run(() => Generator.Run(repo.LoadConfig()));

    private static void AssertRevision(TestRepo repo, string relativePath, int expected)
    {
        Assert.Contains($"<!-- docsync-revision: {expected} -->", repo.Read(relativePath));
    }

    private static JsonElement ReadStatus(TestRepo repo)
    {
        using var document = JsonDocument.Parse(repo.Read("docs/.sync-status.json"));
        return document.RootElement.Clone();
    }

    private static TestRepo CreateGeneratedPairRepo(params string[] pairIds)
    {
        var repo = TestRepo.Create();
        foreach (var pairId in pairIds)
        {
            repo.Write($"docs/{pairId}.zh.md", TestRepo.Header(pairId) + $"# {pairId} zh\n");
            repo.Write($"docs/{pairId}.en.md", TestRepo.Header(pairId) + $"# {pairId} en\n");
        }

        repo.InitGit();
        repo.CommitAll("docs: add raw pairs");
        RunGenerator(repo);
        repo.CommitAll("docs: generate baseline");
        return repo;
    }

    [Fact]
    public void Generate_NewTranslationJoiningExistingPair_CatchesUpToPeer()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# README zh\n");
        repo.InitGit();
        repo.CommitAll("docs: add zh-only pair");
        RunGenerator(repo);
        repo.CommitAll("docs: generate zh-only baseline");

        // Two zh commits arrive before the English translation exists.
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "zh change 1\n");
        repo.CommitAll("docs: zh change 1");
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "zh change 2\n");
        repo.CommitAll("docs: zh change 2");
        RunGenerator(repo);
        AssertRevision(repo, "docs/README.zh.md", 3);
        Assert.Equal(
            "missing-en",
            ReadStatus(repo).GetProperty("pairs").GetProperty("README").GetProperty("status").GetString());
        repo.CommitAll("docs: generate zh-ahead state");

        // Adding the translation later must make it adopt revision 3 rather
        // than starting at 1 and creating a false mismatch.
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# README en\n");
        repo.CommitAll("docs: add English translation");

        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", 3);
        AssertRevision(repo, "docs/README.en.md", 3);
        var pair = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        Assert.Equal("synced", pair.GetProperty("status").GetString());
        Assert.Equal(3, pair.GetProperty("revisions").GetProperty("en").GetInt32());
        Assert.Equal(0, ConsoleOutputCapture.Run(() => Validator.Run(repo.LoadConfig())).Result);
    }

    [Fact]
    public void Generate_LeadingLanguageKeepsDriftingAhead()
    {
        using var repo = CreateGeneratedPairRepo("README");

        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "zh drift 1\n");
        repo.CommitAll("docs: zh drift 1");
        RunGenerator(repo);
        AssertRevision(repo, "docs/README.zh.md", 2);
        AssertRevision(repo, "docs/README.en.md", 1);
        repo.CommitAll("docs: generate first drift");

        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "zh drift 2\n");
        repo.CommitAll("docs: zh drift 2");
        RunGenerator(repo);

        // The stale translation must stay stale until it is actually touched.
        AssertRevision(repo, "docs/README.zh.md", 3);
        AssertRevision(repo, "docs/README.en.md", 1);
        var pair = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        Assert.Equal("zh-ahead", pair.GetProperty("status").GetString());
        Assert.Equal(3, pair.GetProperty("revisions").GetProperty("zh").GetInt32());
        Assert.Equal(1, pair.GetProperty("revisions").GetProperty("en").GetInt32());
    }

    [Fact]
    public void Generate_InjectsMissingMetadataHeadersInGitRepo()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", "# README zh\n\nBody.\n");
        repo.Write("docs/README.en.md", "# README en\n\nBody.\n");
        repo.InitGit();
        repo.CommitAll("docs: add metadata-less pair");

        RunGenerator(repo);

        Assert.StartsWith("<!-- docsync-pair: README -->", repo.Read("docs/README.zh.md"));
        Assert.StartsWith("<!-- docsync-pair: README -->", repo.Read("docs/README.en.md"));
        AssertRevision(repo, "docs/README.zh.md", 1);
        AssertRevision(repo, "docs/README.en.md", 1);
        Assert.Contains("由 DocSyncTool", repo.Read("docs/README.zh.md"));
        Assert.Contains("managed automatically by DocSyncTool", repo.Read("docs/README.en.md"));
        Assert.Equal(0, ConsoleOutputCapture.Run(() => Validator.Run(repo.LoadConfig())).Result);
    }

    [Fact]
    public void Generate_UnrelatedPairIsNotTouchedByAnotherPairsCommits()
    {
        using var repo = CreateGeneratedPairRepo("README", "META");
        var metaBefore = ReadStatus(repo).GetProperty("pairs").GetProperty("META").GetRawText();

        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "README change\n");
        repo.CommitAll("docs: change README only");
        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", 2);
        AssertRevision(repo, "docs/META.zh.md", 1);
        AssertRevision(repo, "docs/META.en.md", 1);
        Assert.Equal(metaBefore, ReadStatus(repo).GetProperty("pairs").GetProperty("META").GetRawText());
    }

    [Fact]
    public void GitRepository_EmptyBlobRequestAndTopologyOrder()
    {
        using var repo = CreateGeneratedPairRepo("README");
        var git = GitRepository.TryCreate(repo.Root);
        Assert.NotNull(git);

        Assert.Empty(git.ReadBlobTexts([]));

        var order = git.GetCommitTopologyOrder();
        Assert.Equal(2, order.Count);
        Assert.Equal(0, order[repo.RunGit("rev-list", "--max-parents=0", "HEAD").Trim()]);
        Assert.Equal(1, order[git.HeadSha]);
    }

    [Fact]
    public void GitRepository_PathHistoryCapturesRenameAndDeletion()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/old.zh.md", "# old\n");
        repo.InitGit();
        repo.CommitAll("docs: add old file");
        repo.RunGit("mv", "docs/old.zh.md", "docs/new.zh.md");
        repo.CommitAll("docs: rename old to new");
        repo.RunGit("rm", "docs/new.zh.md");
        repo.CommitAll("docs: delete new file");

        var git = GitRepository.TryCreate(repo.Root);
        Assert.NotNull(git);

        var history = git.GetPathHistory("docs/new.zh.md");
        Assert.Equal(3, history.Count);

        // Newest first: deletion, rename, original add.
        Assert.Null(history[0].AfterPath);
        Assert.Equal("docs/old.zh.md", history[1].BeforePath);
        Assert.Equal("docs/new.zh.md", history[1].AfterPath);
        Assert.Equal("docs/old.zh.md", history[2].AfterPath);
    }

    [Fact]
    public void RevisionTracker_UncommittedSnapshotWithEqualHash_ReturnsNoEvents()
    {
        using var repo = CreateGeneratedPairRepo("README");
        var git = GitRepository.TryCreate(repo.Root);
        Assert.NotNull(git);

        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "uncommitted snapshot\n");
        var content = repo.Read("docs/README.zh.md");
        var uncommittedHash = ContentHash.Compute(content);

        // The hash matches the working tree but is not in git history yet;
        // this is the idempotent second local generate before commit.
        var events = new RevisionTracker(git).GetContentEvents(
            "docs/README.zh.md",
            content,
            uncommittedHash);

        Assert.Empty(events);
    }

    [Fact]
    public void Generate_StatusV2RecordsAndPreservesContentHashes()
    {
        using var repo = CreateGeneratedPairRepo("README");
        var pair = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        var zhHash = pair.GetProperty("content_hashes").GetProperty("zh").GetString();
        var enHash = pair.GetProperty("content_hashes").GetProperty("en").GetString();
        Assert.False(string.IsNullOrWhiteSpace(zhHash));
        Assert.False(string.IsNullOrWhiteSpace(enHash));

        RunGenerator(repo);

        pair = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        Assert.Equal(zhHash, pair.GetProperty("content_hashes").GetProperty("zh").GetString());
        Assert.Equal(enHash, pair.GetProperty("content_hashes").GetProperty("en").GetString());
    }
}
