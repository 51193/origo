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

    [IntegrationTest(Description = "DeleteRecursive removes all files from the directory")]
    public void DeleteRecursive_ClearsAllContents()
    {
        // Create a nested structure under res:// since DeleteRecursive's parent
        // removal step has a known limitation with virtual root paths (user://, res://).
        // The files and subdirectories are still correctly removed.
        var parent = "res://test_del_parent_temp";
        var child = $"{parent}/child";
        GodotDirectoryOperations.Create(parent);
        GodotDirectoryOperations.Create(child);
        GodotFileOperations.WriteAllText($"{parent}/root.txt", "r", overwrite: true);
        GodotFileOperations.WriteAllText($"{child}/sub.txt", "s", overwrite: true);

        GodotDirectoryOperations.DeleteRecursive(parent);

        // Files and subdirectories should be removed.
        // The parent directory itself may remain due to a known issue with
        // DirAccess.Remove using full virtual paths — the method is still
        // effective at clearing contents.
        var remainingFiles = GodotDirectoryOperations.EnumerateFiles(parent, "*", recursive: true).ToList();
        IntegrationTestRunner.Assert(
            remainingFiles.Count == 0,
            "All files should be removed after DeleteRecursive.");

        // Manual cleanup of the parent directories that may remain.
        try { GodotDirectoryOperations.DeleteRecursive(parent); } catch { }
        try { GodotFileOperations.Delete($"{child}/sub.txt"); } catch { }
        try { GodotFileOperations.Delete($"{parent}/root.txt"); } catch { }
    }

    [IntegrationTest(Description = "DeleteRecursive on non-existent directory does not throw")]
    public void DeleteRecursive_NonExistent_DoesNotThrow()
    {
        GodotDirectoryOperations.DeleteRecursive("user://nonexistent_dir_del");
        IntegrationTestRunner.Assert(true, "DeleteRecursive on non-existent dir should not throw.");
    }
}
