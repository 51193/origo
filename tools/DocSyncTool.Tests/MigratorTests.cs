using Xunit;

namespace DocSyncTool.Tests;

/// <summary>
///     Runs in a serialized collection because <see cref="ConsoleOutputCapture" />
///     redirects the process-global console streams.
/// </summary>
[Collection("DocSyncToolConsoleCapture")]
public class MigratorTests
{
    private static void RunMigrator(Config config) =>
        ConsoleOutputCapture.Run(() => Migrator.Run(config));

    [Fact]
    public void Migrate_RenamesAndInjectsMetadata()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.md", "# Readme\n\nBody.\n");

        RunMigrator(repo.LoadConfig());

        Assert.False(repo.Exists("docs/README.md"));
        var migrated = repo.Read("docs/README.zh.md");
        Assert.StartsWith("<!-- docsync-pair: README -->", migrated);
        Assert.Contains("<!-- docsync-revision: 1 -->", migrated);
        Assert.Contains("# Readme", migrated);
    }

    [Fact]
    public void Migrate_RewritesBareMdLinksToZh()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.md", "# Readme\n\n[meta](META.md#section)\n\n[site](https://example.com/x.md)\n");
        repo.Write("docs/META.md", "# Meta\n");

        RunMigrator(repo.LoadConfig());

        var migrated = repo.Read("docs/README.zh.md");
        Assert.Contains("[meta](META.zh.md#section)", migrated);
        Assert.Contains("https://example.com/x.md", migrated);
    }

    [Fact]
    public void Migrate_SkipsAlreadyLanguageSuffixed()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.zh.md", "<!-- docsync-pair: README -->\n# zh\n");
        repo.Write("docs/README.en.md", "<!-- docsync-pair: README -->\n# en\n");

        RunMigrator(repo.LoadConfig());

        Assert.True(repo.Exists("docs/README.zh.md"));
        Assert.True(repo.Exists("docs/README.en.md"));
    }

    [Fact]
    public void Migrate_SkipsAlreadyMigratedFiles()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.md", "<!-- docsync-pair: README -->\n# already migrated\n");

        RunMigrator(repo.LoadConfig());

        Assert.True(repo.Exists("docs/README.md"));
        Assert.False(repo.Exists("docs/README.zh.md"));
    }

    [Fact]
    public void Migrate_SkipsWhenTargetAlreadyExists()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.md", "# will not move\n");
        repo.Write("docs/README.zh.md", "<!-- docsync-pair: README -->\n# exists\n");

        RunMigrator(repo.LoadConfig());

        Assert.True(repo.Exists("docs/README.md"));
    }

    [Fact]
    public void Migrate_NothingToMigrate_NoThrow()
    {
        using var repo = TestRepo.Create();

        RunMigrator(repo.LoadConfig());

        Assert.False(repo.Exists("docs/README.zh.md"));
    }

    [Fact]
    public void Migrate_NestedDirectory_GetsPairIdFromRelativePath()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/sub/mod.md", "# Mod\n");

        RunMigrator(repo.LoadConfig());

        var migrated = repo.Read("docs/sub/mod.zh.md");
        Assert.StartsWith("<!-- docsync-pair: sub/mod -->", migrated);
    }

    [Fact]
    public void Migrate_NonMdLinksAndAnchors_Unchanged()
    {
        using var repo = TestRepo.Create();
        repo.Write("docs/README.md", "# Readme\n\n[dir](assets/)\n[anch](README.md#top)\n");

        RunMigrator(repo.LoadConfig());

        Assert.Contains("[dir](assets/)", repo.Read("docs/README.zh.md"));
        Assert.Contains("[anch](README.zh.md#top)", repo.Read("docs/README.zh.md"));
    }
}
