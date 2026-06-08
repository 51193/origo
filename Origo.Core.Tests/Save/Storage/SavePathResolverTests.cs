using System;
using Origo.Core.Save.Storage;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SaveFileHandleTests
{
    [Fact]
    public void SavePathResolver_EnsureParentDirectory_CreatesParent()
    {
        var fs = new TestFileSystem();
        var parentDir = fs.GetParentDirectory("root/sub/file.txt");
        if (!string.IsNullOrEmpty(parentDir) && !fs.DirectoryExists(parentDir))
            fs.CreateDirectory(parentDir);
        Assert.True(fs.DirectoryExists("root/sub"));
    }

    [Fact]
    public void SavePathResolver_EnsureParentDirectory_NoOpForRootFile()
    {
        var fs = new TestFileSystem();
        var parentDir = fs.GetParentDirectory("file.txt");
        if (!string.IsNullOrEmpty(parentDir) && !fs.DirectoryExists(parentDir))
            fs.CreateDirectory(parentDir);
        Assert.False(fs.DirectoryExists("file.txt"));
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExtractsRelative()
    {
        var handle = new SaveFileHandle(new TestFileSystem(), "root/saves");
        var result = handle.GetRelativePath("root/saves/file.json");
        Assert.Equal("file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NestedPath()
    {
        var handle = new SaveFileHandle(new TestFileSystem(), "root");
        var result = handle.GetRelativePath("root/sub/file.json");
        Assert.Equal("sub/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExactMatch_ReturnsEmpty()
    {
        var handle = new SaveFileHandle(new TestFileSystem(), "root/saves");
        var result = handle.GetRelativePath("root/saves");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NoMatch_ReturnsFullPath()
    {
        var handle = new SaveFileHandle(new TestFileSystem(), "root/a");
        var result = handle.GetRelativePath("root/b/file.json");
        Assert.Equal("root/b/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_RejectsTraversalInRelativeSegment()
    {
        var handle = new SaveFileHandle(new TestFileSystem(), "root/saves");
        Assert.Throws<ArgumentException>(() =>
            handle.GetRelativePath("root/saves/../evil.json"));
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_WhitespaceRoot_ThrowsOnConstruction()
    {
        Assert.Throws<ArgumentException>(() => new SaveFileHandle(new TestFileSystem(), ""));
        Assert.Throws<ArgumentException>(() => new SaveFileHandle(new TestFileSystem(), "  "));
    }

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_ReturnsLastSegment() =>
        Assert.Equal("child", SaveFileHandle.GetLeafDirectoryName("root/parent/child"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_SingleSegment() =>
        Assert.Equal("single", SaveFileHandle.GetLeafDirectoryName("single"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_TrailingSlash() =>
        Assert.Equal("child", SaveFileHandle.GetLeafDirectoryName("root/child/"));

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SaveFileHandle.GetLeafDirectoryName(""));
        Assert.Equal(string.Empty, SaveFileHandle.GetLeafDirectoryName("  "));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_ThrowsOnDotDot()
    {
        Assert.Throws<ArgumentException>(() => SaveFileHandle.RejectPathTraversal("../evil"));
        Assert.Throws<ArgumentException>(() => SaveFileHandle.RejectPathTraversal("some/../evil"));
        Assert.Throws<ArgumentException>(() => SaveFileHandle.RejectPathTraversal(".."));
        Assert.Throws<ArgumentException>(() => SaveFileHandle.RejectPathTraversal("path/.."));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_AllowsSafePaths()
    {
        var ex = Record.Exception(() =>
        {
            SaveFileHandle.RejectPathTraversal("safe/path");
            SaveFileHandle.RejectPathTraversal("file.json");
            SaveFileHandle.RejectPathTraversal("");
        });
        Assert.Null(ex);
    }
}
