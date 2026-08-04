using System.Linq;
using Godot;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotDirectoryOperationsIntegrationTests
{
    [IntegrationTest(Description = "Create directory and verify Exists returns true")]
    public void Create_And_Exists_ReturnsTrue()
    {
        var dir = "user://test_dir_ops_dir";

        GodotDirectoryOperations.Create(dir);
        IntegrationTestRunner.Assert(
            GodotDirectoryOperations.Exists(dir),
            "Directory should exist after creation.");

        // Cleanup with explicit recursive delete
        using var da = DirAccess.Open("user://");
        da?.Remove("test_dir_ops_dir");
    }

    [IntegrationTest(Description = "EnumerateFiles returns only matching files")]
    public void EnumerateFiles_ReturnsMatchingFiles()
    {
        var dir = "user://test_dir_enum";
        GodotDirectoryOperations.Create(dir);
        GodotFileOperations.WriteAllText("user://test_dir_enum/a.txt", "a", overwrite: true);
        GodotFileOperations.WriteAllText("user://test_dir_enum/b.txt", "b", overwrite: true);
        GodotFileOperations.WriteAllText("user://test_dir_enum/c.dat", "c", overwrite: true);

        var txtFiles = GodotDirectoryOperations.EnumerateFiles(dir, "*.txt", recursive: false).ToList();
        IntegrationTestRunner.AssertEqual(2, txtFiles.Count, "txt file count");

        // Cleanup
        using var da = DirAccess.Open("user://");
        da?.Remove("test_dir_enum/a.txt");
        using var da2 = DirAccess.Open("user://");
        da2?.Remove("test_dir_enum/b.txt");
        using var da3 = DirAccess.Open("user://");
        da3?.Remove("test_dir_enum/c.dat");
        using var da4 = DirAccess.Open("user://");
        da4?.Remove("test_dir_enum");
    }

    [IntegrationTest(Description = "EnumerateFiles non-recursive skips subdirectory contents")]
    public void EnumerateFiles_NonRecursive_SkipsSubdirs()
    {
        var dir = "user://test_dir_nonrec";
        var sub = "user://test_dir_nonrec/sub";
        GodotDirectoryOperations.Create(dir);
        GodotDirectoryOperations.Create(sub);
        GodotFileOperations.WriteAllText($"{dir}/root.txt", "r", overwrite: true);
        GodotFileOperations.WriteAllText($"{sub}/sub.txt", "s", overwrite: true);

        var files = GodotDirectoryOperations.EnumerateFiles(dir, "*.txt", recursive: false).ToList();
        IntegrationTestRunner.AssertEqual(1, files.Count, "non-recursive file count");

        // Cleanup
        using var da = DirAccess.Open("user://");
        da?.Remove("test_dir_nonrec/sub/sub.txt");
        using var da2 = DirAccess.Open("user://");
        da2?.Remove("test_dir_nonrec/sub");
        using var da3 = DirAccess.Open("user://");
        da3?.Remove("test_dir_nonrec/root.txt");
        using var da4 = DirAccess.Open("user://");
        da4?.Remove("test_dir_nonrec");
    }

    [IntegrationTest(Description = "EnumerateFiles recursive includes subdirectory contents")]
    public void EnumerateFiles_Recursive_IncludesSubdirs()
    {
        var dir = "user://test_dir_rec";
        var sub = "user://test_dir_rec/sub";
        GodotDirectoryOperations.Create(dir);
        GodotDirectoryOperations.Create(sub);
        GodotFileOperations.WriteAllText($"{dir}/root.txt", "r", overwrite: true);
        GodotFileOperations.WriteAllText($"{sub}/sub.txt", "s", overwrite: true);

        var files = GodotDirectoryOperations.EnumerateFiles(dir, "*.txt", recursive: true).ToList();
        IntegrationTestRunner.AssertEqual(2, files.Count, "recursive file count");

        // Cleanup
        using var da = DirAccess.Open("user://");
        da?.Remove("test_dir_rec/sub/sub.txt");
        using var da2 = DirAccess.Open("user://");
        da2?.Remove("test_dir_rec/sub");
        using var da3 = DirAccess.Open("user://");
        da3?.Remove("test_dir_rec/root.txt");
        using var da4 = DirAccess.Open("user://");
        da4?.Remove("test_dir_rec");
    }

    [IntegrationTest(Description = "EnumerateDirectories returns subdirectories")]
    public void EnumerateDirectories_ReturnsSubdirs()
    {
        var dir = "user://test_dir_enum_dirs";
        GodotDirectoryOperations.Create(dir);
        GodotDirectoryOperations.Create($"{dir}/sub1");
        GodotDirectoryOperations.Create($"{dir}/sub2");

        var dirs = GodotDirectoryOperations.EnumerateDirectories(dir).ToList();
        IntegrationTestRunner.Assert(dirs.Count >= 2, "Should enumerate at least 2 subdirectories.");

        // Cleanup
        using var da = DirAccess.Open("user://");
        da?.Remove("test_dir_enum_dirs/sub1");
        using var da2 = DirAccess.Open("user://");
        da2?.Remove("test_dir_enum_dirs/sub2");
        using var da3 = DirAccess.Open("user://");
        da3?.Remove("test_dir_enum_dirs");
    }

    [IntegrationTest(Description = "DeleteRecursive clears the directory contents but leaves the container intact")]
    public void DeleteRecursive_RemovesDirectory()
    {
        var dir = "res://test_dir_delete";
        GodotDirectoryOperations.Create(dir);
        GodotFileOperations.WriteAllText($"{dir}/f.txt", "x", overwrite: true);

        GodotDirectoryOperations.DeleteRecursive(dir);

        IntegrationTestRunner.Assert(
            GodotDirectoryOperations.Exists(dir),
            "Directory container should still exist after DeleteRecursive.");
        IntegrationTestRunner.AssertEmpty(
            GodotDirectoryOperations.EnumerateFiles(dir, "*", false),
            "files in directory");
        IntegrationTestRunner.AssertEmpty(
            GodotDirectoryOperations.EnumerateDirectories(dir),
            "subdirectories in directory");

        // Cleanup: remove the empty container.
        using var da = DirAccess.Open("res://");
        var rmErr = da!.Remove("test_dir_delete");
        IntegrationTestRunner.Assert(rmErr == Error.Ok, $"Cleanup should succeed: {rmErr}");
    }

    [IntegrationTest(Description = "DeleteRecursive clears a user:// directory contents including subdirectories, leaves all containers intact")]
    public void DeleteRecursive_UserPath_RemovesDirectory()
    {
        var dir = "user://test_dir_delete_user";
        GodotDirectoryOperations.Create(dir);
        GodotFileOperations.WriteAllText($"{dir}/f.txt", "x", overwrite: true);
        GodotDirectoryOperations.Create($"{dir}/sub");
        GodotFileOperations.WriteAllText($"{dir}/sub/f2.txt", "x", overwrite: true);

        GodotDirectoryOperations.DeleteRecursive(dir);

        IntegrationTestRunner.Assert(
            GodotDirectoryOperations.Exists(dir),
            "user:// directory container should still exist after DeleteRecursive.");
        IntegrationTestRunner.AssertEmpty(
            GodotDirectoryOperations.EnumerateFiles(dir, "*", false),
            "files in user:// directory");

        // Cleanup: remove the empty containers (parent + sub).
        GodotDirectoryOperations.DeleteRecursive($"{dir}/sub");
        using var da = DirAccess.Open("user://");
        da!.Remove("test_dir_delete_user/sub");
        da.Remove("test_dir_delete_user");
    }

    [IntegrationTest(Description = "DeleteRecursive on non-existent directory does not throw")]
    public void DeleteRecursive_NonExistent_DoesNotThrow()
    {
        GodotDirectoryOperations.DeleteRecursive("user://nonexistent_dir_del");
        IntegrationTestRunner.Assert(true, "DeleteRecursive on non-existent dir should not throw.");
    }

    [IntegrationTest(Description = "DeleteRecursive removes hidden (dot-prefixed) files")]
    public void DeleteRecursive_RemovesHiddenFiles()
    {
        // The write-in-progress marker is a dot-prefixed file; a cleanup that
        // skips hidden files would leave it behind and fail strict readers.
        var dir = "user://test_dir_hidden_del";
        GodotDirectoryOperations.Create(dir);
        GodotFileOperations.WriteAllText($"{dir}/.write_in_progress", "", overwrite: true);
        GodotFileOperations.WriteAllText($"{dir}/visible.txt", "v", overwrite: true);

        GodotDirectoryOperations.DeleteRecursive(dir);

        IntegrationTestRunner.Assert(
            !GodotFileOperations.Exists($"{dir}/.write_in_progress"),
            "hidden files must be removed by DeleteRecursive");
        IntegrationTestRunner.Assert(
            !GodotFileOperations.Exists($"{dir}/visible.txt"),
            "visible files must be removed by DeleteRecursive");
    }

    [IntegrationTest(Description = "EnumerateFiles includes hidden (dot-prefixed) files")]
    public void EnumerateFiles_IncludesHiddenFiles()
    {
        var dir = "user://test_dir_hidden_enum";
        GodotDirectoryOperations.Create(dir);
        GodotFileOperations.WriteAllText($"{dir}/.hidden.txt", "h", overwrite: true);
        GodotFileOperations.WriteAllText($"{dir}/visible.txt", "v", overwrite: true);

        var files = GodotDirectoryOperations.EnumerateFiles(dir, "*", recursive: false).ToList();

        IntegrationTestRunner.AssertContains(
            "hidden.txt", string.Join(",", files),
            "hidden files must be enumerated");

        // Cleanup
        GodotDirectoryOperations.DeleteRecursive(dir);
        using var da = DirAccess.Open("user://");
        da!.Remove("test_dir_hidden_enum");
    }
}
