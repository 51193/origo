using System;
using System.Linq;
using Godot;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

/// <summary>
///     Tests for <see cref="IntegrationTestRunner.CleanupTestUserData" />:
///     the runner must start every test process from a clean user data
///     directory so that artifacts left by an abnormally ended previous
///     process (e.g. a write-in-progress marker from an interrupted save)
///     cannot fail strict readers in the next run.
/// </summary>
public class UserDataCleanupIntegrationTests
{
    [IntegrationTest(Description = "Cleanup removes a leftover write-in-progress marker from an interrupted save")]
    public void Cleanup_RemovesInterruptedSaveMarker()
    {
        var fs = new GodotFileSystem();

        // Simulate the residue of a previous test process that was killed
        // mid-save: the strict reader would reject this marker (fail-fast).
        fs.CreateDirectory("user://test_saves/current");
        fs.WriteAllText("user://test_saves/current/.write_in_progress", "", overwrite: true);
        fs.WriteAllText("user://entry.json", "{}", overwrite: true);
        fs.WriteAllText("user://main_menu.json", "[]", overwrite: true);

        IntegrationTestRunner.CleanupTestUserData();

        IntegrationTestRunner.Assert(
            !fs.Exists("user://test_saves/current/.write_in_progress"),
            "write-in-progress marker should be removed by cleanup");
        IntegrationTestRunner.Assert(
            !fs.Exists("user://entry.json"),
            "root-level test artifacts should be removed by cleanup");
        IntegrationTestRunner.Assert(
            !fs.Exists("user://main_menu.json"),
            "root-level test artifacts should be removed by cleanup");
    }

    [IntegrationTest(Description = "Cleanup removes prefixed file-system test artifacts")]
    public void Cleanup_RemovesPrefixedTestArtifacts()
    {
        var fs = new GodotFileSystem();

        fs.CreateDirectory("user://test_dir_leftover/sub");
        fs.WriteAllText("user://test_dir_leftover/sub/a.txt", "a", overwrite: true);
        fs.WriteAllText("user://integration_test_leftover.txt", "x", overwrite: true);

        IntegrationTestRunner.CleanupTestUserData();

        IntegrationTestRunner.Assert(
            !fs.Exists("user://test_dir_leftover/sub/a.txt"),
            "test_-prefixed directory contents should be removed by cleanup");
        IntegrationTestRunner.Assert(
            !fs.Exists("user://integration_test_leftover.txt"),
            "integration_test_-prefixed files should be removed by cleanup");
        IntegrationTestRunner.Assert(
            !fs.DirectoryExists("user://test_dir_leftover"),
            "test_-prefixed directories should be removed entirely by cleanup");
    }

    [IntegrationTest(Description = "Cleanup preserves Godot system content and non-test artifacts")]
    public void Cleanup_PreservesSystemAndNonTestContent()
    {
        var fs = new GodotFileSystem();

        fs.CreateDirectory("user://keep_dir");
        fs.WriteAllText("user://keep_dir/keep.txt", "keep", overwrite: true);
        fs.WriteAllText("user://keep_root.txt", "keep", overwrite: true);

        try
        {
            IntegrationTestRunner.CleanupTestUserData();

            IntegrationTestRunner.Assert(
                fs.Exists("user://keep_dir/keep.txt"),
                "non-test directories must survive cleanup");
            IntegrationTestRunner.Assert(
                fs.Exists("user://keep_root.txt"),
                "non-test files must survive cleanup");
        }
        finally
        {
            // These artifacts are not test-prefixed, so no cleanup would ever
            // remove them; the test removes its own residue, including the
            // empty keep_dir container. Cleanup failures are logged, never
            // thrown from finally (they would mask the test verdict).
            try
            {
                fs.Delete("user://keep_root.txt");
                fs.Delete("user://keep_dir/keep.txt");
                fs.DeleteDirectory("user://keep_dir");
                using var root = DirAccess.Open("user://");
                var err = root.Remove("keep_dir");
                if (err != Error.Ok)
                    GD.PrintErr($"Failed to remove keep_dir container: {err}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to remove keep_dir test residue: {ex.Message}");
            }
        }
    }

    [IntegrationTest(Description = "Cleanup is idempotent")]
    public void Cleanup_IsIdempotent()
    {
        IntegrationTestRunner.CleanupTestUserData();
        IntegrationTestRunner.CleanupTestUserData();
        IntegrationTestRunner.Assert(true, "cleanup should be safe to call repeatedly");
    }

    [IntegrationTest(Description = "Cleanup keeps the Godot system logs directory untouched")]
    public void Cleanup_PreservesLogsDirectory()
    {
        var fs = new GodotFileSystem();

        // The Godot engine owns user://logs; the cleanup must neither delete
        // nor create it — its presence is unchanged before and after cleanup.
        var logsExistedBefore = fs.DirectoryExists("user://logs");

        IntegrationTestRunner.CleanupTestUserData();

        IntegrationTestRunner.AssertEqual(
            logsExistedBefore,
            fs.DirectoryExists("user://logs"),
            "Godot logs directory presence must be unchanged by cleanup");
    }
}
