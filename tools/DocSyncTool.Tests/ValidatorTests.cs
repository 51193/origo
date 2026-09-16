using System.Linq;
using Xunit;

namespace DocSyncTool.Tests;

/// <summary>
///     Runs in a serialized collection because <see cref="ConsoleErrorCapture" />
///     redirects the process-global <c>Console.Error</c> stream.
/// </summary>
[Collection("DocSyncToolConsoleCapture")]
public class ValidatorTests
{

    private static int RunValidator(Config config) =>
        ConsoleOutputCapture.Run(() => Validator.Run(config)).Result;

    private static void WriteSyncedPair(TestRepo repo, string pairId, string relativePath, int revision = 1)
    {
        var lang = relativePath.EndsWith(".zh.md", System.StringComparison.Ordinal) ? "zh" : "en";
        repo.Write(relativePath, TestRepo.Header(pairId, revision) + $"# {pairId}\n\nSome {lang} content.\n");
    }

    [Fact]
    public void Validate_FullySyncedTree_ReturnsZero()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_RevisionMismatch_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md", 1);
        WriteSyncedPair(repo, "README", "docs/README.en.md", 2);

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingLanguageFile_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_CrossLanguageLink_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        WriteSyncedPair(repo, "a/README", "docs/a/README.zh.md");
        WriteSyncedPair(repo, "a/README", "docs/a/README.en.md");
        repo.Write("docs/a/README.zh.md", TestRepo.Header("a/README") + "# A\n\n[target](README.en.md)\n");

        var exitCode = RunValidator(repo.LoadConfig());
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Validate_BareMdLink_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[target](META.zh.md)\n");
        repo.Write("docs/META.zh.md", TestRepo.Header("META") + "# META\n\n## Section\n");
        repo.Write("docs/META.en.md", TestRepo.Header("META") + "# META\n\n## Section\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# B\n\n[target](META.md)\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_BrokenLink_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[missing](nonexistent.zh.md)\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingPairHeader_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", "# No metadata here\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingRevisionHeader_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", "<!-- docsync-pair: README -->\n# No revision\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingReminderComment_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md",
            "<!-- docsync-pair: README -->\n<!-- docsync-revision: 1 -->\n# No reminder\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_PairIdNotMatchingPath_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        repo.Write("docs/README.en.md", TestRepo.Header("WRONG/PAIR") + "# B\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_InvalidRevisionValue_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md",
            "<!-- docsync-pair: README -->\n<!-- docsync-revision: abc -->\n" +
            "<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->\n# A\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_CodeBlockLinks_Ignored()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n```md\n[link](README.en.md)\n```\n\n`inline [x](META.md)`\n");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_ExternalUrls_Ignored()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n[https://example.com/x.md](https://example.com/x.md)\n");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_SameLanguageLinks_Ok()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        WriteSyncedPair(repo, "META", "docs/META.zh.md");
        WriteSyncedPair(repo, "META", "docs/META.en.md");
        repo.Write("docs/META.zh.md", TestRepo.Header("META") + "# META\n\n## Section\n");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[meta](META.zh.md#section)\n");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_LanguageLinkEscapingDocsMirror_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        // Resolves outside docs/ (to the repo-root benchmarks/ directory) —
        // the mirror is self-contained, so a language-suffixed target there
        // is always broken.
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n[baseline](../../benchmarks/baseline.zh.md)\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_BareMdLinkEscapingDocsMirror_Allowed()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        // Bare .md links may legitimately point at repo-root documents
        // (e.g. AGENTS.md referenced from META docs).
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[AGENTS](../AGENTS.md)\n");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_LanguageLinkToDocsSiblingDirectory_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        // "docs-backup" merely starts with "docs"; a raw prefix comparison
        // would wrongly accept it as inside the mirror.
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n[stale](../docs-backup/stale.zh.md)\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_DirectoryLinkOk()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        System.IO.Directory.CreateDirectory(repo.Full("docs/child"));
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[child](child/)\n");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_DirectoryLinkBroken_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[missing](missing/)\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_BrokenAnchor_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        WriteSyncedPair(repo, "META", "docs/META.zh.md");
        WriteSyncedPair(repo, "META", "docs/META.en.md");
        repo.Write("docs/META.zh.md", TestRepo.Header("META") + "# META\n");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[meta](META.zh.md#missing)\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_ReferenceDefinitionBrokenTarget_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n[target][meta]\n\n[meta]: missing.zh.md\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_RevisionMovedBackwards_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md", 2);
        WriteSyncedPair(repo, "README", "docs/README.en.md", 2);
        repo.Write("docs/.sync-status.json",
            """{"schema_version":1,"languages":["zh","en"],"pairs":{"README":{"status":"synced","revisions":{"zh":2,"en":2},"previous_revisions":{"zh":3,"en":3},"files":{"zh":"README.zh.md","en":"README.en.md"}}}}""");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_ReferenceLinkWithoutDefinition_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[target][missing]\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_DuplicateReferenceDefinition_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n[target][meta]\n\n[meta]: META.zh.md\n[meta]: META.zh.md\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_CollapsedReferenceWithoutDefinition_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[target][]\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_SameFileAnchorOk()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n## Section\n\n[go](#section)\n");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_SameFileAnchorBroken_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[go](#missing)\n");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MalformedStatusSnapshot_IsIgnored()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/.sync-status.json", "{not-json");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_HeadingParityMismatch_WarnsButPasses()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n\n### B\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n");

        var (exitCode, warnings) = ConsoleOutputCapture.Run(() =>
        {
            var code = Validator.RunCore(repo.LoadConfig(), out var w);
            return (code, w);
        }).Result;

        Assert.Equal(0, exitCode);
        Assert.Contains(warnings, w => w.Contains("heading structure differs"));
    }

    [Fact]
    public void Validate_HeadingParityMatch_NoWarning()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n");

        var (exitCode, warnings) = ConsoleOutputCapture.Run(() =>
        {
            var code = Validator.RunCore(repo.LoadConfig(), out var w);
            return (code, w);
        }).Result;

        Assert.Equal(0, exitCode);
        Assert.Empty(warnings);
    }
    [Fact]
    public void Validate_SourceDirectoryWithoutDocMirror_Fails()
    {
        using var repo = TestRepo.Create();
        ConfigureSourceMirror(repo, []);
        repo.Write("Origo.Core/Widget.cs", "public sealed class Widget { }");
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_SourceFileNotListedInDocMirror_Fails()
    {
        using var repo = TestRepo.Create();
        ConfigureSourceMirror(repo, []);
        repo.Write("Origo.Core/Widget.cs", "public sealed class Widget { }");
        WriteSyncedPair(repo, "Origo.Core/README", "docs/Origo.Core/README.zh.md");
        WriteSyncedPair(repo, "Origo.Core/README", "docs/Origo.Core/README.en.md");

        Assert.Equal(1, RunValidator(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_SourceMirrorOverride_Passes()
    {
        using var repo = TestRepo.Create();
        ConfigureSourceMirror(
            repo,
            [("Origo.TestSupport/Metadata", "docs/Origo.TestSupport/Architecture")]);
        repo.Write("Origo.TestSupport/Metadata/Widget.cs", "internal sealed class Widget { }");
        WriteSyncedPair(repo, "Origo.TestSupport/Architecture/README", "docs/Origo.TestSupport/Architecture/README.zh.md");
        WriteSyncedPair(repo, "Origo.TestSupport/Architecture/README", "docs/Origo.TestSupport/Architecture/README.en.md");
        repo.Write(
            "docs/Origo.TestSupport/Architecture/README.zh.md",
            TestRepo.Header("Origo.TestSupport/Architecture/README") + "# Architecture\n\n`Widget.cs`\n");
        repo.Write(
            "docs/Origo.TestSupport/Architecture/README.en.md",
            TestRepo.Header("Origo.TestSupport/Architecture/README") + "# Architecture\n\n`Widget.cs`\n");

        Assert.Equal(0, RunValidator(repo.LoadConfig()));
    }

    private static void ConfigureSourceMirror(
        TestRepo repo,
        (string SourceDir, string DocDir)[] overrides)
    {
        var overrideEntries = string.Join(
            ",",
            overrides.Select(o => $"\"{o.SourceDir}\":\"{o.DocDir}\""));
        var json = "{\"languages\":[\"zh\",\"en\"]," +
            "\"DocsRoot\":\"docs\"," +
            "\"SourceMirrorRoots\":[\"Origo.TestSupport\"]," +
            $"\"SourceDocOverrides\":{{{overrideEntries}}}}}";
        repo.Write("tools/DocSyncTool/docsync-config.json", json);
    }
}
