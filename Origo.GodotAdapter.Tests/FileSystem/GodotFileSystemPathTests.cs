using System;
using Origo.GodotAdapter.FileSystem;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class GodotFileSystemPathTests
{
    [Fact]
    public void GodotFileSystem_CombinePath_UsesHelperRules()
    {
        var fs = new GodotFileSystem();
        var combined = fs.CombinePath("user://save", "slot_001/progress.json");
        Assert.Equal("user://save/slot_001/progress.json", combined);
    }

    [Fact]
    public void GodotFileSystem_GetParentDirectory_UsesHelperRules()
    {
        var fs = new GodotFileSystem();
        var parent = fs.GetParentDirectory("user://save/current/progress.json");
        Assert.Equal("user://save/current", parent);
    }

    [Fact]
    public void GodotFileSystem_CombinePath_NullSecondArg_ReturnsFirst()
    {
        var fs = new GodotFileSystem();
        var result = fs.CombinePath("user://base", null!);
        Assert.Equal("user://base", result);
    }
}
