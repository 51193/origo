using System;
using Origo.Core.Save.Storage;
using Xunit;

namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SavePathLayoutTests
{
    public static TheoryData<string, string> GetSaveDirectory_FormatsCorrectly_Data { get; } = new()
    {
        { "001", "save_001" },
        { "abc", "save_abc" },
        { "my-save", "save_my-save" }
    };

    public static TheoryData<string?> GetSaveDirectory_ThrowsOnInvalidId_Data { get; } = CreateInvalidSaveIds();

    public static TheoryData<string, string> GetLevelDirectory_ThrowsOnInvalidArgs_Data { get; } = new()
    {
        { "", "level1" },
        { "base", "" },
        { "", "" },
        { "base", "../evil" },
        { "base", "level/one" },
        { "base", "level one" },
        { "base", "中文关卡" }
    };

    [Fact]
    public void SavePathLayout_GetCurrentDirectory_ReturnsCurrent() =>
        Assert.Equal("current", SavePathLayout.GetCurrentDirectory());

    [Fact]
    public void SavePathLayout_CurrentDirectoryName_Constant() =>
        Assert.Equal("current", SavePathLayout.CurrentDirectoryName);

    [Theory]
    [MemberData(nameof(GetSaveDirectory_FormatsCorrectly_Data))]
    public void SavePathLayout_GetSaveDirectory_FormatsCorrectly(string saveId, string expected) =>
        Assert.Equal(expected, SavePathLayout.GetSaveDirectory(saveId));

    [Theory]
    [MemberData(nameof(GetSaveDirectory_ThrowsOnInvalidId_Data))]
    public void SavePathLayout_GetSaveDirectory_ThrowsOnInvalidId(string? saveId) =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetSaveDirectory(saveId!));

    [Fact]
    public void SavePathLayout_GetProgressFile_CombinesCorrectly() =>
        Assert.Equal("mybase/progress.json", SavePathLayout.GetProgressFile("mybase"));

    [Fact]
    public void SavePathLayout_GetProgressFile_ThrowsOnEmpty() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetProgressFile(""));

    [Fact]
    public void SavePathLayout_GetProgressStateMachinesFile_CombinesCorrectly() =>
        Assert.Equal("base/progress_state_machines.json", SavePathLayout.GetProgressStateMachinesFile("base"));

    [Fact]
    public void SavePathLayout_GetProgressStateMachinesFile_ThrowsOnWhitespace() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetProgressStateMachinesFile("  "));

    [Fact]
    public void SavePathLayout_GetCustomMetaFile_CombinesCorrectly() =>
        Assert.Equal("base/meta.map", SavePathLayout.GetCustomMetaFile("base"));

    [Fact]
    public void SavePathLayout_GetCustomMetaFile_ThrowsOnNull() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetCustomMetaFile(null!));

    [Fact]
    public void SavePathLayout_GetLevelDirectory_CombinesCorrectly() =>
        Assert.Equal("base/level_town", SavePathLayout.GetLevelDirectory("base", "town"));

    [Theory]
    [MemberData(nameof(GetLevelDirectory_ThrowsOnInvalidArgs_Data))]
    public void SavePathLayout_GetLevelDirectory_ThrowsOnInvalidArgs(string baseDir, string levelId) =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelDirectory(baseDir, levelId));

    [Fact]
    public void SavePathLayout_GetLevelSndSceneFile_CombinesCorrectly()
    {
        Assert.Equal("level_dir/snd_scene.json",
            SavePathLayout.GetLevelSndSceneFile("level_dir"));
    }

    [Fact]
    public void SavePathLayout_GetLevelSndSceneFile_ThrowsOnEmpty() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSndSceneFile(""));

    [Fact]
    public void SavePathLayout_GetLevelSessionFile_CombinesCorrectly()
    {
        Assert.Equal("level_dir/session.json",
            SavePathLayout.GetLevelSessionFile("level_dir"));
    }

    [Fact]
    public void SavePathLayout_GetLevelSessionFile_ThrowsOnWhitespace() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSessionFile("   "));

    [Fact]
    public void SavePathLayout_GetLevelSessionStateMachinesFile_CombinesCorrectly()
    {
        Assert.Equal("level_dir/session_state_machines.json",
            SavePathLayout.GetLevelSessionStateMachinesFile("level_dir"));
    }

    [Fact]
    public void SavePathLayout_GetLevelSessionStateMachinesFile_ThrowsOnNull() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSessionStateMachinesFile(null!));

    [Fact]
    public void SavePathLayout_GetWriteInProgressMarker_CombinesCorrectly()
    {
        Assert.Equal("base/.write_in_progress",
            SavePathLayout.GetWriteInProgressMarker("base"));
    }

    [Fact]
    public void SavePathLayout_GetWriteInProgressMarker_ThrowsOnEmpty() =>
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetWriteInProgressMarker(""));

    [Fact]
    public void SavePathLayout_WriteInProgressMarkerName_Constant() =>
        Assert.Equal(".write_in_progress", SavePathLayout.WriteInProgressMarkerName);

    private static TheoryData<string?> CreateInvalidSaveIds()
    {
        var d = new TheoryData<string?>
        {
            default(string?),
            "",
            "   "
        };
        return d;
    }
}

// ── SavePathResolver ───────────────────────────────────────────────────
