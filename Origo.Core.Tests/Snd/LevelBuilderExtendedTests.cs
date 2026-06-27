using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class LevelBuilderExtendedTests
{
    [Fact]
    public void Build_ProducesLevelPayload()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        builder.AddEntity(MakeMeta("entity_a"));
        builder.SetSessionData("key1", "val1");

        var payload = builder.Build();

        Assert.Equal("lvl1", payload.LevelId);
        Assert.False(string.IsNullOrWhiteSpace(TestFactory.JsonFromNode(payload.SndSceneNode)));
        Assert.False(string.IsNullOrWhiteSpace(TestFactory.JsonFromNode(payload.SessionNode)));
        Assert.False(string.IsNullOrWhiteSpace(TestFactory.JsonFromNode(payload.SessionStateMachinesNode)));
    }

    [Fact]
    public void Build_ThenModify_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.AddEntity(MakeMeta("x")));
        Assert.Throws<InvalidOperationException>(() => builder.SetSessionData("k", 1));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Commit_WritesToFileSystem()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.AddEntity(MakeMeta("e1"));

        var payload = builder.Commit();
        Assert.Equal("lvl1", payload.LevelId);
        Assert.True(fs.Exists("root/current/level_lvl1/snd_scene.json"));
    }

    [Fact]
    public void AddEntity_DuplicateName_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.AddEntity(MakeMeta("dup"));

        Assert.Throws<InvalidOperationException>(() => builder.AddEntity(MakeMeta("dup")));
    }

    [Fact]
    public void AddEntity_NullMeta_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentNullException>(() => builder.AddEntity(null!));
    }

    [Fact]
    public void AddEntity_EmptyName_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentException>(() => builder.AddEntity(new SndMetaData
        {
            Name = "",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        }));
    }

    [Fact]
    public void AddEntities_BatchAdd()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        builder.AddEntities(new[] { MakeMeta("a"), MakeMeta("b"), MakeMeta("c") });
        Assert.Equal(3, builder.SceneHost.GetEntities().Count);
    }

    [Fact]
    public void AddEntities_NullList_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentNullException>(() => builder.AddEntities(null!));
    }

    [Fact]
    public void AddEntityFromTemplate_ClonesAndAdds()
    {
        var logger = new TestLogger();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        fs.SeedFile("maps/templates.map", "tmpl: templates/tmpl.json");
        fs.SeedFile("templates/tmpl.json",
            """
            {
              "name": "TemplateEntity",
              "node": { "pairs": {} },
              "strategy": { "lifecycle_indices": [] },
              "data": { "pairs": {} }
            }
            """);
        var sndWorld = TestFactory.CreateSndWorld(logger: logger, fileSystem: fs);
        sndWorld.LoadTemplates("maps/templates.map", logger);

        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        builder.AddEntityFromTemplate("tmpl", "overridden");
        Assert.NotNull(builder.SceneHost.FindByName("overridden"));
    }

    [Fact]
    public void AddEntityFromTemplate_EmptyKey_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);

        Assert.Throws<ArgumentException>(() => builder.AddEntityFromTemplate(""));
    }

    [Fact]
    public void Constructor_EmptyLevelId_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        Assert.Throws<ArgumentException>(() => new LevelBuilder("", sndWorld, storage));
    }

    [Fact]
    public void Constructor_NullSndWorld_Throws()
    {
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        Assert.Throws<ArgumentNullException>(() => new LevelBuilder("lvl", null!, storage));
    }

    [Fact]
    public void Constructor_NullStorage_Throws()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        Assert.Throws<ArgumentNullException>(() => new LevelBuilder("lvl", sndWorld, null!));
    }

    [Fact]
    public void SessionBlackboard_IsAccessible()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("lvl1", sndWorld, storage);
        builder.SetSessionData("key", 42);

        var (found, val) = builder.SessionBlackboard.TryGet<int>("key");
        Assert.True(found);
        Assert.Equal(42, val);
    }

    [Fact]
    public void LevelId_ExposesConstructedValue()
    {
        var sndWorld = TestFactory.CreateSndWorld();
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storage = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var builder = new LevelBuilder("my_level", sndWorld, storage);
        Assert.Equal("my_level", builder.LevelId);
    }

    private static SndMetaData MakeMeta(string name)
    {
        return new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };
    }

    private static (IFileMetaAccess MetaAccess, IDataSourceIoGateway DataSourceIo, IPathResolver PathResolver)
        CreateGateways(IFileSystem fs) =>
        (DataSourceFactory.CreateFileMetaAccess(fs),
         DataSourceFactory.CreateDefaultIoGateway(fs),
         DataSourceFactory.CreatePathResolver(fs));
}

// ─────────────────────────────────────────────────────────────────────────────
// 8. SndCountCommandHandler — exercise ExecuteCore via console pipeline
// ─────────────────────────────────────────────────────────────────────────────
