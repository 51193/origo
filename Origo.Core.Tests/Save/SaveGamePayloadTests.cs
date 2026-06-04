using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

public class SaveGamePayloadTests
{
    [Fact]
    public void CurrentFormatVersion_IsOne() => Assert.Equal(1, SaveGamePayload.CurrentFormatVersion);

    [Fact]
    public void DefaultValues()
    {
        var payload = new SaveGamePayload();
        Assert.Equal(SaveGamePayload.CurrentFormatVersion, payload.FormatVersion);
        Assert.Equal(string.Empty, payload.SaveId);
        Assert.Equal(string.Empty, payload.ActiveLevelId);
        Assert.True(payload.ProgressNode.IsNull);
        Assert.NotNull(payload.Levels);
    }

    [Fact]
    public void LevelPayload_DefaultValues()
    {
        var lp = new LevelPayload();
        Assert.Equal(string.Empty, lp.LevelId);
        Assert.True(lp.SndSceneNode.IsNull);
        Assert.True(lp.SessionNode.IsNull);
        Assert.True(lp.SessionStateMachinesNode.IsNull);
    }

    [Fact]
    public void WithSingleLevel_CanAccessLevel()
    {
        var payload = new SaveGamePayload
        {
            SaveId = "test",
            ActiveLevelId = "level_1",
            Levels = new Dictionary<string, LevelPayload>
            {
                ["level_1"] = new()
                {
                    LevelId = "level_1",
                    SndSceneNode = DataSourceNode.CreateArray(),
                    SessionNode = DataSourceNode.CreateObject(),
                    SessionStateMachinesNode = DataSourceNode.CreateObject()
                }
            }
        };

        Assert.Equal("test", payload.SaveId);
        Assert.Equal("level_1", payload.ActiveLevelId);
        Assert.True(payload.Levels.ContainsKey("level_1"));
        Assert.Equal("level_1", payload.Levels["level_1"].LevelId);
    }

    [Fact]
    public void WithMultipleLevels_AllAccessible()
    {
        var payload = new SaveGamePayload
        {
            SaveId = "multi",
            ActiveLevelId = "core",
            Levels = new Dictionary<string, LevelPayload>
            {
                ["core"] = new() { LevelId = "core" },
                ["dungeon"] = new() { LevelId = "dungeon" },
                ["town"] = new() { LevelId = "town" }
            }
        };

        Assert.Equal(3, payload.Levels.Count);
        Assert.Contains("core", payload.Levels.Keys);
        Assert.Contains("dungeon", payload.Levels.Keys);
        Assert.Contains("town", payload.Levels.Keys);
    }

    [Fact]
    public void CustomMeta_CanBeSet()
    {
        var payload = new SaveGamePayload
        {
            CustomMeta = new Dictionary<string, string>
            {
                ["play_time"] = "1h",
                ["difficulty"] = "hard"
            }
        };

        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("1h", payload.CustomMeta["play_time"]);
        Assert.Equal("hard", payload.CustomMeta["difficulty"]);
    }

    [Fact]
    public void CustomMeta_Null_Allowed()
    {
        var payload = new SaveGamePayload { CustomMeta = null };

        Assert.Null(payload.CustomMeta);
    }
}
