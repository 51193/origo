using System;
using Godot;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotRuntimeSmokeTests
{
    [IntegrationTest(Description = "GD.Print works in headless mode")]
    public void GDPrint_Works_InHeadlessMode()
    {
        GD.Print("IntegrationTestRunner: Godot headless runtime smoke test.");
        IntegrationTestRunner.Assert(true, "GD.Print should not throw.");
    }

    [IntegrationTest(Description = "FileAccess static class is available")]
    public void FileAccess_Static_IsAvailable()
    {
        IntegrationTestRunner.Assert(
            FileAccess.FileExists("res://project.godot"),
            "project.godot should exist in res:// root.");
        IntegrationTestRunner.Assert(
            !FileAccess.FileExists("res://nonexistent_file_xyz.dat"),
            "Non-existent file should report as not existing.");
    }

    [IntegrationTest(Description = "DirAccess static class is available")]
    public void DirAccess_Static_IsAvailable()
    {
        IntegrationTestRunner.Assert(
            DirAccess.DirExistsAbsolute("res://"),
            "res:// should be an existing directory.");
        IntegrationTestRunner.Assert(
            !DirAccess.DirExistsAbsolute("res://nonexistent_dir_xyz/"),
            "Non-existent directory should report as not existing.");
    }

    [IntegrationTest(Description = "Godot.Vector2 type is available in runtime")]
    public void Vector2_Type_IsAvailable()
    {
        var v = new Vector2(1.5f, 2.5f);
        IntegrationTestRunner.Assert(
            Math.Abs(v.X - 1.5f) < 0.001f && Math.Abs(v.Y - 2.5f) < 0.001f,
            "Vector2 should have correct coordinate values.");
    }

    [IntegrationTest(Description = "SceneTree is accessible from Engine main loop")]
    public void SceneTree_IsAccessible()
    {
        var tree = Engine.GetMainLoop();
        IntegrationTestRunner.Assert(tree is SceneTree, "Main loop should be a SceneTree.");
    }
}
