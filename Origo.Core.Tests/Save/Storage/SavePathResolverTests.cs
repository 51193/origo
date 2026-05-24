using System;
using Origo.Core.Save.Storage;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SavePathResolverTests
{
    [Fact]
    public void SavePathResolver_EnsureParentDirectory_CreatesParent()
    {
        var fs = new TestFileSystem();
        SavePathResolver.EnsureParentDirectory(fs, "root/sub/file.txt");
        Assert.True(fs.DirectoryExists("root/sub"));
    }

    [Fact]
    public void SavePathResolver_EnsureParentDirectory_NoOpForRootFile()
    {
        var fs = new TestFileSystem();
        var ex = Record.Exception(() => SavePathResolver.EnsureParentDirectory(fs, "file.txt"));
        Assert.Null(ex);
        Assert.False(fs.DirectoryExists("file.txt"));
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExtractsRelative()
    {
        var result = SavePathResolver.GetRelativePath("root/saves", "root/saves/file.json");
        Assert.Equal("file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NestedPath()
    {
        var result = SavePathResolver.GetRelativePath("root", "root/sub/file.json");
        Assert.Equal("sub/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExactMatch_ReturnsEmpty()
    {
        var result = SavePathResolver.GetRelativePath("root/saves", "root/saves");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NoMatch_ReturnsFullPath()
    {
        var result = SavePathResolver.GetRelativePath("root/a", "root/b/file.json");
        Assert.Equal("root/b/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_RejectsTraversalInRelativeSegment()
    {
        Assert.Throws<ArgumentException>(() =>
            SavePathResolver.GetRelativePath("root/saves", "root/saves/../evil.json"));
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_EmptyBase_ReturnsFullPath()
    {
        var result = SavePathResolver.GetRelativePath("", "root/file.json");
        Assert.Equal("root/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_ReturnsLastSegment() =>
        Assert.Equal("child", SavePathResolver.GetLeafDirectoryName("root/parent/child"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_SingleSegment() =>
        Assert.Equal("single", SavePathResolver.GetLeafDirectoryName("single"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_TrailingSlash() =>
        Assert.Equal("child", SavePathResolver.GetLeafDirectoryName("root/child/"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SavePathResolver.GetLeafDirectoryName(""));
        Assert.Equal(string.Empty, SavePathResolver.GetLeafDirectoryName("  "));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_ThrowsOnDotDot()
    {
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("../evil"));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("some/../evil"));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal(".."));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("path/.."));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_AllowsSafePaths()
    {
        var ex = Record.Exception(() =>
        {
            SavePathResolver.RejectPathTraversal("safe/path");
            SavePathResolver.RejectPathTraversal("file.json");
            SavePathResolver.RejectPathTraversal("");
        });
        Assert.Null(ex);
    }
}

// ── SaveMetaMapCodec ───────────────────────────────────────────────────
