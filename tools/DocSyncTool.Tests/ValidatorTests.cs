using Xunit;

namespace DocSyncTool.Tests;

public class ValidatorTests
{
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

        Assert.Equal(0, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_RevisionMismatch_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md", 1);
        WriteSyncedPair(repo, "README", "docs/README.en.md", 2);

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingLanguageFile_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
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

        var exitCode = Validator.Run(repo.LoadConfig());
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Validate_BareMdLink_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[target](META.zh.md)\n");
        repo.Write("docs/META.zh.md", TestRepo.Header("META") + "# META\n");
        repo.Write("docs/META.en.md", TestRepo.Header("META") + "# META\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# B\n\n[target](META.md)\n");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_BrokenLink_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[missing](nonexistent.zh.md)\n");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingPairHeader_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", "# No metadata here\n");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingRevisionHeader_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", "<!-- docsync-pair: README -->\n# No revision\n");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_MissingReminderComment_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md",
            "<!-- docsync-pair: README -->\n<!-- docsync-revision: 1 -->\n# No reminder\n");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_PairIdNotMatchingPath_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        repo.Write("docs/README.en.md", TestRepo.Header("WRONG/PAIR") + "# B\n");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_InvalidRevisionValue_Fails()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md",
            "<!-- docsync-pair: README -->\n<!-- docsync-revision: abc -->\n" +
            "<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->\n# A\n");

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_CodeBlockLinks_Ignored()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n```md\n[link](README.en.md)\n```\n\n`inline [x](META.md)`\n");

        Assert.Equal(0, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_ExternalUrls_Ignored()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") +
            "# A\n\n[https://example.com/x.md](https://example.com/x.md)\n");

        Assert.Equal(0, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_SameLanguageLinks_Ok()
    {
        using var repo = TestRepo.Create();
        WriteSyncedPair(repo, "README", "docs/README.zh.md");
        WriteSyncedPair(repo, "README", "docs/README.en.md");
        WriteSyncedPair(repo, "META", "docs/META.zh.md");
        WriteSyncedPair(repo, "META", "docs/META.en.md");
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# A\n\n[meta](META.zh.md#section)\n");

        Assert.Equal(0, Validator.Run(repo.LoadConfig()));
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

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
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

        Assert.Equal(0, Validator.Run(repo.LoadConfig()));
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

        Assert.Equal(1, Validator.Run(repo.LoadConfig()));
    }

    [Fact]
    public void Validate_HeadingParityMismatch_WarnsButPasses()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n\n### B\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n");

        var exitCode = Validator.RunCore(repo.LoadConfig(), out var warnings);

        Assert.Equal(0, exitCode);
        Assert.Contains(warnings, w => w.Contains("heading structure differs"));
    }

    [Fact]
    public void Validate_HeadingParityMatch_NoWarning()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n");
        repo.Write("docs/README.en.md", TestRepo.Header("README") + "# R\n\n## Design\n\n### A\n");

        var exitCode = Validator.RunCore(repo.LoadConfig(), out var warnings);

        Assert.Equal(0, exitCode);
        Assert.Empty(warnings);
    }
}
