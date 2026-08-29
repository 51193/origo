using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace DocSyncTool.Tests;

/// <summary>
///     Exercises the git-derived revision planner in a real temporary git
///     repository. These tests run serialized with the other DocSyncTool
///     tests because generator output is captured through the process-global
///     console streams.
/// </summary>
[Collection("DocSyncToolConsoleCapture")]
public class GitRevisionTests
{
    private static void RunGenerator(TestRepo repo) =>
        ConsoleOutputCapture.Run(() => Generator.Run(repo.LoadConfig()));

    private static TestRepo CreateGeneratedGitRepo()
    {
        var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# README zh\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# README en\n");
        repo.InitGit();
        repo.CommitAll("docs: add raw pair");
        RunGenerator(repo);
        repo.CommitAll("docs: generate baseline");
        return repo;
    }

    private static JsonElement ReadStatus(TestRepo repo)
    {
        using var document = JsonDocument.Parse(repo.Read("docs/.sync-status.json"));
        return document.RootElement.Clone();
    }

    private static void AssertRevision(TestRepo repo, string relativePath, int expected)
    {
        Assert.Contains($"<!-- docsync-revision: {expected} -->", repo.Read(relativePath));
    }

    [Fact]
    public void ContentHash_IgnoresDocSyncMetadataAndNormalizesNewlines()
    {
        const string body = "# Title\n\nSome body text.\n";
        var zh = TestRepo.Header("README", 3) + body;
        var en = "<!-- docsync-pair: README -->\n" +
                 "<!-- docsync-revision: 99 -->\n" +
                 "<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->\n" +
                 body;

        Assert.Equal(ContentHash.Compute(zh), ContentHash.Compute(en));
        Assert.Equal(ContentHash.Compute(body), ContentHash.Compute(body.Replace("\n", "\r\n")));
        Assert.True(ContentHash.IsDocSyncMetadataLine("<!-- docsync-revision: 1 -->"));
        Assert.False(ContentHash.IsDocSyncMetadataLine("# Title"));
    }

    [Fact]
    public void GitRepository_ReadsPathHistoryAndMissingBlobs()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", "# track me\n");
        repo.InitGit();
        var first = repo.CommitAll("docs: add tracked file");
        repo.Write("docs/README.zh.md", "# track me\n\nchanged\n");
        var second = repo.CommitAll("docs: change tracked file");

        var git = GitRepository.TryCreate(repo.Root);
        Assert.NotNull(git);
        Assert.Equal(second, git.HeadSha);

        var history = git.GetPathHistory("docs/README.zh.md");
        Assert.Equal([second, first], history.Select(h => h.Sha));
        Assert.All(history, h => Assert.Equal("docs/README.zh.md", h.AfterPath));

        var blobs = git.ReadBlobTexts([(second, "docs/README.zh.md"), (first, "docs/missing.md")]);
        Assert.Contains("changed", blobs[0]);
        Assert.Null(blobs[1]);
    }

    [Fact]
    public void GitRepository_TryCreate_ReturnsNullOutsideRepository()
    {
        using var repo = TestRepo.Create();
        Assert.Null(GitRepository.TryCreate(repo.Root));
    }

    [Fact]
    public void RevisionTracker_UnknownOldHash_PlansPendingChange()
    {
        // This is the local-generate-before-commit fallback: the snapshot
        // hash is not in git history yet, so a different working-tree
        // content is planned as exactly one pending change.
        using var repo = CreateGeneratedGitRepo();
        var git = GitRepository.TryCreate(repo.Root);
        Assert.NotNull(git);

        var tracker = new RevisionTracker(git);
        var events = tracker.GetContentEvents(
            "docs/README.zh.md",
            repo.Read("docs/README.zh.md"),
            "not-a-real-content-hash");

        var pending = Assert.Single(events);
        Assert.Null(pending.CommitSha);
    }

    [Fact]
    public void Generate_CountsEveryCommitSinceLastSnapshot()
    {
        using var repo = CreateGeneratedGitRepo();

        // Two zh-only commits are pushed before generate runs. The final CI
        // checkout must not collapse them into one revision bump.
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "zh change 1\n");
        repo.CommitAll("docs: zh change 1");
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "zh change 2\n");
        repo.CommitAll("docs: zh change 2");

        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", 3);
        AssertRevision(repo, "docs/README.en.md", 1);
        var status = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        Assert.Equal("zh-ahead", status.GetProperty("status").GetString());
        Assert.Equal(3, status.GetProperty("revisions").GetProperty("zh").GetInt32());
        Assert.Equal(1, status.GetProperty("revisions").GetProperty("en").GetInt32());

        var afterFirstRun = repo.Read("docs/.sync-status.json");
        RunGenerator(repo);
        Assert.Equal(afterFirstRun, repo.Read("docs/.sync-status.json"));

        // Commit the generated state, then let a single translation commit
        // catch up to zh revision 3.
        repo.CommitAll("docs: generate after zh edits");
        repo.Write("docs/README.en.md", repo.Read("docs/README.en.md") + "en translation\n");
        repo.CommitAll("docs: translate zh changes");

        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", 3);
        AssertRevision(repo, "docs/README.en.md", 3);
        status = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        Assert.Equal("synced", status.GetProperty("status").GetString());
        Assert.Equal(3, status.GetProperty("revisions").GetProperty("zh").GetInt32());
        Assert.Equal(3, status.GetProperty("revisions").GetProperty("en").GetInt32());

        afterFirstRun = repo.Read("docs/.sync-status.json");
        RunGenerator(repo);
        Assert.Equal(afterFirstRun, repo.Read("docs/.sync-status.json"));
    }

    [Fact]
    public void Generate_MetadataOnlyCommitDoesNotAdvanceRevision()
    {
        using var repo = CreateGeneratedGitRepo();

        var before = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        var oldRevision = before.GetProperty("revisions").GetProperty("zh").GetInt32();

        // Change only the reminder text, which ContentHash deliberately
        // ignores. This simulates an auto-generated commit following a real
        // content commit and must not create an auto-commit loop.
        var content = repo.Read("docs/README.zh.md");
        var reminderLine = content.Split('\n')
            .First(line => line.TrimStart().StartsWith("<!-- docsync-revision ", StringComparison.Ordinal));
        repo.Write("docs/README.zh.md", content.Replace(
            reminderLine,
            "<!-- docsync-revision — an ignored metadata-only edit. -->"));
        repo.CommitAll("docs: auto-sync metadata only");

        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", oldRevision);
        var after = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        Assert.Equal(
            before.GetProperty("revisions").GetProperty("zh").GetInt32(),
            after.GetProperty("revisions").GetProperty("zh").GetInt32());
    }

    [Fact]
    public void Generate_BothLanguagesInOneCommit_AdvanceTogether()
    {
        using var repo = CreateGeneratedGitRepo();
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "pair change\n");
        repo.Write("docs/README.en.md", repo.Read("docs/README.en.md") + "pair change\n");
        repo.CommitAll("docs: update both languages");

        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", 2);
        AssertRevision(repo, "docs/README.en.md", 2);
        Assert.Equal(
            "synced",
            ReadStatus(repo).GetProperty("pairs").GetProperty("README").GetProperty("status").GetString());
    }

    [Fact]
    public void Generate_NewPairInGitRepoStartsAtRevisionOne()
    {
        using var repo = CreateGeneratedGitRepo();
        repo.Write("docs/META.zh.md", TestRepo.Header("META") + "# META zh\n");
        repo.Write("docs/META.en.md", TestRepo.Header("META") + "# META en\n");
        repo.CommitAll("docs: add META pair");

        RunGenerator(repo);

        var status = ReadStatus(repo).GetProperty("pairs").GetProperty("META");
        Assert.Equal("synced", status.GetProperty("status").GetString());
        Assert.Equal(1, status.GetProperty("revisions").GetProperty("zh").GetInt32());
        Assert.Equal(1, status.GetProperty("revisions").GetProperty("en").GetInt32());
    }

    [Fact]
    public void Generate_LegacyV1Status_PreservesExistingManualBump()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README", 1) + "# README zh\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README", 1) + "# README en\n");
        repo.Write("docs/.sync-status.json",
            """{"schema_version":1,"languages":["zh","en"],"pairs":{"README":{"status":"synced","revisions":{"zh":1,"en":1},"previous_revisions":{},"files":{"zh":"README.zh.md","en":"README.en.md"}}}}""");
        repo.InitGit();
        repo.CommitAll("docs: legacy v1 snapshot");

        // The old manual workflow: content changed and the author already
        // bumped the header before generate. The one-time v1 migration must
        // preserve revision 2 rather than add another generation.
        repo.Write("docs/README.zh.md", TestRepo.Header("README", 2) + "# README zh\n\nchanged zh\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README", 2) + "# README en\n\nchanged en\n");

        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", 2);
        AssertRevision(repo, "docs/README.en.md", 2);
        var pair = ReadStatus(repo).GetProperty("pairs").GetProperty("README");
        Assert.Equal(2, pair.GetProperty("revisions").GetProperty("zh").GetInt32());
        Assert.Equal(2, pair.GetProperty("revisions").GetProperty("en").GetInt32());
    }

    [Fact]
    public void Generate_LegacyV1Status_CountsForgottenBumpOnce()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README", 1) + "# README zh\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README", 1) + "# README en\n");
        repo.Write("docs/.sync-status.json",
            """{"schema_version":1,"languages":["zh","en"],"pairs":{"README":{"status":"synced","revisions":{"zh":1,"en":1},"previous_revisions":{},"files":{"zh":"README.zh.md","en":"README.en.md"}}}}""");
        repo.InitGit();
        repo.CommitAll("docs: legacy v1 snapshot");

        // Content changed but the header was not bumped. The automatic
        // planner counts the working-tree change exactly once during
        // migration.
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "changed zh\n");
        repo.Write("docs/README.en.md", repo.Read("docs/README.en.md") + "changed en\n");

        RunGenerator(repo);

        AssertRevision(repo, "docs/README.zh.md", 2);
        AssertRevision(repo, "docs/README.en.md", 2);
    }

    [Fact]
    public void Generate_TwoUncommittedEdits_CountsLatestDeltaOnce()
    {
        using var repo = CreateGeneratedGitRepo();

        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "local edit 1\n");
        RunGenerator(repo);
        AssertRevision(repo, "docs/README.zh.md", 2);

        // A second edit before the first generated state is committed still
        // advances the planned revision by exactly one more generation.
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "local edit 2\n");
        RunGenerator(repo);
        AssertRevision(repo, "docs/README.zh.md", 3);

        var afterSecondRun = repo.Read("docs/.sync-status.json");
        RunGenerator(repo);
        Assert.Equal(afterSecondRun, repo.Read("docs/.sync-status.json"));
        AssertRevision(repo, "docs/README.zh.md", 3);
    }

    [Fact]
    public void Generate_UncommittedChangeIsPlannedIdempotently()
    {
        using var repo = CreateGeneratedGitRepo();

        // Simulate the local pre-commit workflow: edit first, generate before
        // committing, then run generate again. Both runs must produce the
        // same planned revision (the synthetic working-tree event is counted
        // exactly once).
        repo.Write("docs/README.zh.md", repo.Read("docs/README.zh.md") + "local edit\n");
        RunGenerator(repo);
        AssertRevision(repo, "docs/README.zh.md", 2);

        var afterFirstRun = repo.Read("docs/.sync-status.json");
        RunGenerator(repo);
        Assert.Equal(afterFirstRun, repo.Read("docs/.sync-status.json"));
        AssertRevision(repo, "docs/README.zh.md", 2);
    }
}
