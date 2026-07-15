using System.IO;
using System.Linq;
using Xunit;

namespace Origo.Core.Tests;

// ── KeyValueFileParser ─────────────────────────────────────────────────

public class TestMemoryFileSystemAdditionalTests
{
    [Fact]
    public void TestMemoryFileSystem_WriteAllText_And_ReadAllText()
    {
        var fs = new TestMemoryFileSystem();
        fs.WriteAllText("root/file.txt", "content", false);
        Assert.Equal("content", fs.ReadAllText("root/file.txt"));
    }

    [Fact]
    public void TestMemoryFileSystem_WriteAllText_Overwrite()
    {
        var fs = new TestMemoryFileSystem();
        fs.WriteAllText("file.txt", "v1", false);
        fs.WriteAllText("file.txt", "v2", true);
        Assert.Equal("v2", fs.ReadAllText("file.txt"));
    }

    [Fact]
    public void TestMemoryFileSystem_WriteAllText_NoOverwrite_Throws()
    {
        var fs = new TestMemoryFileSystem();
        fs.WriteAllText("file.txt", "v1", false);
        Assert.Throws<IOException>(() => fs.WriteAllText("file.txt", "v2", false));
    }

    [Fact]
    public void TestMemoryFileSystem_Delete_RemovesFile()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("file.txt", "data");
        Assert.True(fs.Exists("file.txt"));
        fs.Delete("file.txt");
        Assert.False(fs.Exists("file.txt"));
    }

    [Fact]
    public void TestMemoryFileSystem_CombinePath_CombinesCorrectly()
    {
        var fs = new TestMemoryFileSystem();
        Assert.Equal("root/sub/file.txt", fs.CombinePath("root/sub", "file.txt"));
    }

    [Fact]
    public void TestMemoryFileSystem_GetParentDirectory()
    {
        var fs = new TestMemoryFileSystem();
        Assert.Equal("root/sub", fs.GetParentDirectory("root/sub/file.txt"));
    }

    [Fact]
    public void TestMemoryFileSystem_EnumerateDirectories()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("root/a/file1.txt", "");
        fs.SeedFile("root/b/file2.txt", "");

        var dirs = fs.EnumerateDirectories("root").ToList();
        Assert.Contains("root/a", dirs);
        Assert.Contains("root/b", dirs);
    }

    [Fact]
    public void TestMemoryFileSystem_Rename_MovesAllFilesAndDirectories()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("src/a.txt", "hello");
        fs.SeedFile("src/sub/b.txt", "world");

        fs.Rename("src", "dst");

        Assert.False(fs.Exists("src/a.txt"));
        Assert.False(fs.Exists("src/sub/b.txt"));
        Assert.True(fs.Exists("dst/a.txt"));
        Assert.True(fs.Exists("dst/sub/b.txt"));
        Assert.Equal("hello", fs.ReadAllText("dst/a.txt"));
        Assert.Equal("world", fs.ReadAllText("dst/sub/b.txt"));
    }

    [Fact]
    public void TestMemoryFileSystem_DeleteDirectory_RemovesAllContents()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("dir/a.txt", "1");
        fs.SeedFile("dir/sub/b.txt", "2");
        fs.CreateDirectory("dir/empty");

        Assert.True(fs.DirectoryExists("dir"));

        fs.DeleteDirectory("dir");

        Assert.False(fs.Exists("dir/a.txt"));
        Assert.False(fs.Exists("dir/sub/b.txt"));
        Assert.False(fs.DirectoryExists("dir"));
        Assert.False(fs.DirectoryExists("dir/empty"));
    }
}
