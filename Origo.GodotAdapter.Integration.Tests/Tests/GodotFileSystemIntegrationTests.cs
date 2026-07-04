using System.Collections.Generic;
using Godot;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotFileSystemIntegrationTests
{
    private readonly GodotFileSystem _fs = new();

    [IntegrationTest(Description = "Write and read a text file in user://")]
    public void WriteRead_UserDir_RoundTrip()
    {
        var path = "user://integration_test_write_read.txt";
        var content = "Hello from integration test!";

        _fs.WriteAllText(path, content, overwrite: true);
        var read = _fs.ReadAllText(path);

        IntegrationTestRunner.AssertEqual(content, read, nameof(content));
    }

    [IntegrationTest(Description = "Write and read a text file in res://")]
    public void WriteRead_ResDir_RoundTrip()
    {
        var path = "res://integration_test_write_read.txt";
        var content = "Hello from res:// test!";

        _fs.WriteAllText(path, content, overwrite: true);
        var read = _fs.ReadAllText(path);

        IntegrationTestRunner.AssertEqual(content, read, nameof(content));

        _fs.Delete(path);
    }

    [IntegrationTest(Description = "Create a directory in user:// and verify it exists")]
    public void CreateDirectory_UserDir_Exists()
    {
        var dir = "user://integration_test_dir";

        _fs.CreateDirectory(dir);
        IntegrationTestRunner.Assert(_fs.DirectoryExists(dir), $"Directory '{dir}' should exist after creation.");

        _fs.DeleteDirectory(dir);
    }

    [IntegrationTest(Description = "Enumerate files in a directory")]
    public void EnumerateFiles_UserDir_ReturnsFiles()
    {
        var dir = "user://integration_test_enum";
        _fs.CreateDirectory(dir);
        _fs.WriteAllText(_fs.CombinePath(dir, "a.txt"), "a", overwrite: true);
        _fs.WriteAllText(_fs.CombinePath(dir, "b.txt"), "b", overwrite: true);

        var files = new List<string>(_fs.EnumerateFiles(dir, "*.txt", recursive: false));
        IntegrationTestRunner.Assert(files.Count == 2, $"Expected 2 files, got {files.Count}.");

        _fs.DeleteDirectory(dir);
    }

    [IntegrationTest(Description = "Delete a file and verify it no longer exists")]
    public void DeleteFile_UserDir_Removed()
    {
        var path = "user://integration_test_delete.txt";
        _fs.WriteAllText(path, "temp", overwrite: true);

        _fs.Delete(path);
        IntegrationTestRunner.Assert(!_fs.Exists(path), $"File '{path}' should not exist after delete.");
    }
}
