using System.Collections.Generic;
using System.IO;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

public class MemoryFileSystemTests
{
    [Fact]
    public void MemoryFileSystem_BasicOperations()
    {
        var fs = new MemoryFileSystem();

        fs.WriteAllText("test/file.txt", "content", false);
        Assert.True(fs.Exists("test/file.txt"));
        Assert.Equal("content", fs.ReadAllText("test/file.txt"));
        Assert.True(fs.DirectoryExists("test"));

        fs.Delete("test/file.txt");
        Assert.False(fs.Exists("test/file.txt"));
    }

    [Fact]
    public void MemoryFileSystem_EnumerateFiles()
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("dir/a.json", "{}", false);
        fs.WriteAllText("dir/b.json", "{}", false);
        fs.WriteAllText("dir/sub/c.json", "{}", false);

        var nonRecursive = new List<string>(fs.EnumerateFiles("dir", "*.json", false));
        Assert.Equal(2, nonRecursive.Count);

        var recursive = new List<string>(fs.EnumerateFiles("dir", "*.json", true));
        Assert.Equal(3, recursive.Count);
    }

    [Fact]
    public void MemoryFileSystem_CombinePath()
    {
        var fs = new MemoryFileSystem();
        Assert.Equal("base/child", fs.CombinePath("base", "child"));
        Assert.Equal("base/child", fs.CombinePath("base/", "child"));
    }

    [Fact]
    public void MemoryFileSystem_Rename()
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("old/file.txt", "data", false);

        fs.Rename("old", "new");

        Assert.True(fs.Exists("new/file.txt"));
        Assert.False(fs.Exists("old/file.txt"));
    }

    [Fact]
    public void MemoryFileSystem_DeleteDirectory()
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("dir/a.txt", "a", false);
        fs.WriteAllText("dir/sub/b.txt", "b", false);

        fs.DeleteDirectory("dir");

        Assert.False(fs.Exists("dir/a.txt"));
        Assert.False(fs.Exists("dir/sub/b.txt"));
        Assert.False(fs.DirectoryExists("dir"));
    }

    [Fact]
    public void MemoryFileSystem_ReadAllText_Missing_ThrowsFileNotFound()
    {
        var fs = new MemoryFileSystem();
        Assert.Throws<FileNotFoundException>(() => fs.ReadAllText("nope.txt"));
    }

    [Fact]
    public void MemoryFileSystem_WriteAllText_NoOverwrite_ThrowsWhenExists()
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("x.txt", "a", false);
        Assert.Throws<IOException>(() => fs.WriteAllText("x.txt", "b", false));
    }

    [Fact]
    public void MemoryFileSystem_Copy_SourceMissing_Throws()
    {
        var fs = new MemoryFileSystem();
        Assert.Throws<FileNotFoundException>(() => fs.Copy("missing.txt", "dest.txt", true));
    }

    [Fact]
    public void MemoryFileSystem_Copy_NoOverwrite_ThrowsWhenDestExists()
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("a.txt", "a", false);
        fs.WriteAllText("b.txt", "b", false);
        Assert.Throws<IOException>(() => fs.Copy("a.txt", "b.txt", false));
    }

    [Fact]
    public void MemoryFileSystem_EnumerateFiles_CustomPatternAndBackslashNormalize()
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("d/x.data", "1", false);
        fs.WriteAllText("d/y.data", "2", false);
        fs.WriteAllText("d/other.txt", "3", false);

        var list = new List<string>(fs.EnumerateFiles("d", "*.data", true));
        Assert.Equal(2, list.Count);

        fs.WriteAllText(@"d\sub\z.data", "z", false);
        Assert.Equal("z", fs.ReadAllText("d/sub/z.data"));
    }

    [Fact]
    public void MemoryFileSystem_CreateDirectory_EmptyPath_NoOp()
    {
        var fs = new MemoryFileSystem();
        var ex = Record.Exception(() =>
        {
            fs.CreateDirectory("");
            fs.CreateDirectory("   ");
        });
        Assert.Null(ex);
        Assert.Empty(new List<string>(fs.EnumerateDirectories("")));
    }

    [Fact]
    public void MemoryFileSystem_GetParentDirectory_EdgeCases()
    {
        var fs = new MemoryFileSystem();
        Assert.Equal("", fs.GetParentDirectory("fileonly"));
        Assert.Equal("", fs.GetParentDirectory("/abs"));
        Assert.Equal("a", fs.GetParentDirectory("a/b"));
    }

    [Fact]
    public void MemoryFileSystem_EnumerateDirectories_FromExplicitDirectories()
    {
        var fs = new MemoryFileSystem();
        fs.CreateDirectory("root/child");
        var dirs = new List<string>(fs.EnumerateDirectories("root"));
        Assert.Contains("root/child", dirs);
    }

    [Fact]
    public void MemoryFileSystem_Rename_FileAtRoot()
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("solo.txt", "x", false);
        fs.Rename("solo.txt", "moved.txt");
        Assert.False(fs.Exists("solo.txt"));
        Assert.Equal("x", fs.ReadAllText("moved.txt"));
    }
}
