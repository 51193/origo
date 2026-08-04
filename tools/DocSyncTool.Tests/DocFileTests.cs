using Xunit;

namespace DocSyncTool.Tests;

public class DocFileTests
{
    [Fact]
    public void ExtractLanguage_StandardSuffix_ReturnsLanguage()
    {
        Assert.Equal("zh", DocFile.ExtractLanguage("README.zh.md"));
        Assert.Equal("en", DocFile.ExtractLanguage("deep/path/README.en.md"));
    }

    [Fact]
    public void ExtractLanguage_NoSuffix_ReturnsEmpty()
    {
        Assert.Equal("", DocFile.ExtractLanguage("README.md"));
    }

    [Fact]
    public void ExtractLanguage_NoDotAtAll_ReturnsEmpty()
    {
        Assert.Equal("", DocFile.ExtractLanguage("README"));
    }

    [Fact]
    public void DerivePairId_NestedPath_StripsLanguageSuffix()
    {
        Assert.Equal("Origo.Core/Snd/README", DocFile.DerivePairId("Origo.Core/Snd/README.zh.md"));
        Assert.Equal("Origo.Core/Snd/README", DocFile.DerivePairId("Origo.Core/Snd/README.en.md"));
    }

    [Fact]
    public void DerivePairId_RootLevel_NoDirectoryPrefix()
    {
        Assert.Equal("META", DocFile.DerivePairId("META.zh.md"));
    }

    [Fact]
    public void DerivePairId_BareMd_StripsMdExtension()
    {
        Assert.Equal("usage/quick-start", DocFile.DerivePairId("usage/quick-start.md"));
    }

    [Fact]
    public void DerivePairId_PathWithForwardSlashes_WorksOnAllPlatforms()
    {
        Assert.Equal("a/b/README", DocFile.DerivePairId("a/b/README.zh.md"));
    }
}
