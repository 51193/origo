using System;
using System.IO;
using Xunit;

namespace DocSyncTool.Tests;

public class ConfigTests
{
    [Fact]
    public void Load_ParsesLanguagesFromConfig()
    {
        using var repo = TestRepo.Create();
        var config = repo.LoadConfig();

        Assert.Equal(["zh", "en"], config.Languages);
        Assert.Equal("docs", config.DocsRoot);
        Assert.Equal(repo.Root, config.RepoRoot);
    }

    [Fact]
    public void Load_MissingConfigFile_Throws()
    {
        using var repo = TestRepo.Create();
        File.Delete(repo.Full("tools/DocSyncTool/docsync-config.json"));

        var ex = Assert.Throws<InvalidOperationException>(() => Config.Load(repo.Root));
        Assert.Contains("Config file not found", ex.Message);
    }

    [Fact]
    public void Load_InvalidJson_ThrowsJsonException()
    {
        using var repo = TestRepo.Create();
        File.WriteAllText(repo.Full("tools/DocSyncTool/docsync-config.json"), "not json");

        Assert.Throws<System.Text.Json.JsonException>(() => Config.Load(repo.Root));
    }

    [Fact]
    public void Load_PascalCaseKeys_AlsoParsed()
    {
        using var repo = TestRepo.Create();
        File.WriteAllText(
            repo.Full("tools/DocSyncTool/docsync-config.json"),
            """{"Languages": ["zh", "en"], "DocsRoot": "docs"}""");

        var config = Config.Load(repo.Root);

        Assert.Equal(["zh", "en"], config.Languages);
    }

    [Theory]
    [InlineData("zh en")]
    [InlineData("zh/en")]
    [InlineData("zh\\\\en")]
    [InlineData("")]
    public void Load_InvalidLanguageCode_Throws(string language)
    {
        using var repo = TestRepo.Create();
        File.WriteAllText(
            repo.Full("tools/DocSyncTool/docsync-config.json"),
            $$"""{"languages":["{{language}}"],"docs_root":"docs"}""");

        var ex = Assert.Throws<InvalidOperationException>(() => Config.Load(repo.Root));
        Assert.Contains("Invalid language code", ex.Message);
    }

    [Fact]
    public void DocsFullPath_CombinesRepoRootAndDocsRoot()
    {
        using var repo = TestRepo.Create();
        var config = repo.LoadConfig();

        Assert.Equal(Path.GetFullPath(Path.Combine(repo.Root, "docs")), config.DocsFullPath);
    }
}
