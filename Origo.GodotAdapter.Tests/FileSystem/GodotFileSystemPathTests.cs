using System;
using Origo.GodotAdapter.FileSystem;
using Xunit;

namespace Origo.GodotAdapter.Tests.FileSystemTests;

public class GodotFileSystemPathTests
{
    public static TheoryData<string> GodotPathResolver_Combine_WithTraversal_Data { get; } = new()
    {
        "../escape",
        "foo/../bar",
        "foo\\..\\bar"
    };

    [Fact]
    public void GodotPathResolver_Combine_JoinsPaths()
    {
        var combined = GodotPathResolver.Combine("user://origo_saves", "current/system.json");
        Assert.Equal("user://origo_saves/current/system.json", combined);
    }

    [Theory]
    [MemberData(nameof(GodotPathResolver_Combine_WithTraversal_Data))]
    public void GodotPathResolver_Combine_WithTraversal_Throws(string relativePath) =>
        Assert.Throws<ArgumentException>(() => GodotPathResolver.Combine("res://root", relativePath));

    [Fact]
    public void GodotPathResolver_GetParentDirectory_HandlesTrailingSlash()
    {
        var parent = GodotPathResolver.GetParentDirectory("res://origo/maps/");
        Assert.Equal("res://origo", parent);
    }

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
}
