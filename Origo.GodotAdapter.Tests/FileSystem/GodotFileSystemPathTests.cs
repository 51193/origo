using System;
using Origo.GodotAdapter.FileSystem;
using Xunit;

namespace Origo.GodotAdapter.Tests.FileSystemTests;

public class GodotFileSystemPathTests
{
    public static TheoryData<string> GodotPathResolver_Combine_WithTraversal_Data { get; } =
    [
        "../escape",
        "foo/../bar",
        "foo\\..\\bar"
    ];

    [Fact]
    public void GodotPathResolver_Combine_WithTrailingDotDotNoSlash_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GodotPathResolver.Combine("res://root", "foo/.."));
    }

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
    public void GodotPathResolver_Combine_NullBasePath_ReturnsRelativePath()
    {
        var result = GodotPathResolver.Combine(null!, "data.json");
        Assert.Equal("data.json", result);
    }

    [Fact]
    public void GodotPathResolver_Combine_EmptyBasePath_ReturnsRelativePath()
    {
        var result = GodotPathResolver.Combine(string.Empty, "data.json");
        Assert.Equal("data.json", result);
    }

    [Fact]
    public void GodotPathResolver_Combine_NullRelativePath_ReturnsBasePath()
    {
        var result = GodotPathResolver.Combine("user://base", null!);
        Assert.Equal("user://base", result);
    }

    [Fact]
    public void GodotPathResolver_Combine_EmptyRelativePath_ReturnsBasePath()
    {
        var result = GodotPathResolver.Combine("user://base", string.Empty);
        Assert.Equal("user://base", result);
    }

    [Fact]
    public void GodotPathResolver_Combine_BothEmpty_ReturnsEmpty()
    {
        var result = GodotPathResolver.Combine(string.Empty, string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GodotPathResolver_Combine_BothNull_ReturnsNull()
    {
        var result = GodotPathResolver.Combine(null!, null!);
        Assert.Null(result);
    }

    [Fact]
    public void GodotPathResolver_GetParentDirectory_EmptyString_ReturnsEmpty()
    {
        var result = GodotPathResolver.GetParentDirectory(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GodotPathResolver_GetParentDirectory_Null_ReturnsEmpty()
    {
        var result = GodotPathResolver.GetParentDirectory(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GodotPathResolver_GetParentDirectory_NoSlash_ReturnsEmpty()
    {
        var result = GodotPathResolver.GetParentDirectory("user_flat_path");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GodotPathResolver_GetParentDirectory_HandlesTrailingSlash()
    {
        var parent = GodotPathResolver.GetParentDirectory("res://origo/maps/");
        Assert.Equal("res://origo", parent);
    }

    [Fact]
    public void GodotPathResolver_GetParentDirectory_RootPath_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GodotPathResolver.GetParentDirectory("/"));
        Assert.Throws<InvalidOperationException>(() =>
            GodotPathResolver.GetParentDirectory("res://"));
        Assert.Throws<InvalidOperationException>(() =>
            GodotPathResolver.GetParentDirectory("user://"));
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

    [Fact]
    public void GodotFileSystem_CombinePath_NullSecondArg_ReturnsFirst()
    {
        var fs = new GodotFileSystem();
        var result = fs.CombinePath("user://base", null!);
        Assert.Equal("user://base", result);
    }
}
