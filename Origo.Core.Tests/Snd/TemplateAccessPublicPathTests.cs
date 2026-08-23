using System.Linq;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Metadata;
using Origo.Core.Serialization;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Public-path tests for the template-entity capability: templates and
///     scene aliases can be loaded through <see cref="ISndTemplateAccess" />,
///     entity lists can be resolved from JSON files (including template
///     shorthand), and the resulting metadata is spawned through
///     <see cref="ISessionRun.Spawn" /> / <see cref="SpawnMany" />.
/// </summary>
public class TemplateAccessPublicPathTests
{
    [Fact]
    public void CloneTemplate_AndSessionSpawn_CreateTemplateEntity()
    {
        var (ctx, fs) = CreateContext("res://maps/snd_templates.map", null);
        fs.SeedFile("res://maps/snd_templates.map", "hero: res://templates/hero.json");
        fs.SeedFile("res://templates/hero.json", """
            {
                "name": "HeroTemplate",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": [] },
                "data": { "pairs": { "hp": { "type": "Int32", "data": 100 } } }
            }
            """);

        ctx.Bootstrap();
        ctx.FlushFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession!;
        var hero = fg.Spawn(ctx.Template.CloneTemplate("hero", "Hero_01"));

        Assert.Equal("Hero_01", hero.Name);
        var (found, hp) = hero.TryGetData<int>("hp");
        Assert.True(found);
        Assert.Equal(100, hp);
    }

    [Fact]
    public void LoadTemplates_AndLoadMetaListFromFile_ResolveTemplateShorthand()
    {
        var (ctx, fs) = CreateContext(null, null);
        fs.SeedFile("res://maps/runtime_templates.map", "late_hero: res://templates/late_hero.json");
        fs.SeedFile("res://templates/late_hero.json", """
            {
                "name": "LateHeroTemplate",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": [] },
                "data": { "pairs": { "speed": { "type": "Single", "data": 12.5 } } }
            }
            """);
        fs.SeedFile("res://levels/late_heroes.json", """
            [
                { "sndName": "LateHero_01", "templateKey": "late_hero" }
            ]
            """);

        ctx.Bootstrap();
        ctx.FlushFrame();

        ctx.Template.LoadTemplates("res://maps/runtime_templates.map");
        var metas = ctx.Template.LoadMetaListFromFile("res://levels/late_heroes.json");

        Assert.Single(metas);
        Assert.Equal("LateHero_01", metas[0].Name);
        Assert.True(metas[0].DataMetaData!.Pairs.ContainsKey("speed"));

        var fg = ctx.Runtime.SessionManager.ForegroundSession!;
        fg.SpawnMany([.. metas]);
        Assert.NotNull(fg.FindByName("LateHero_01"));
    }

    [Fact]
    public void LoadSceneAliases_LoadsAliasMap_ThroughPublicTemplateAccess()
    {
        var (ctx, fs) = CreateContext(null, null);
        fs.SeedFile("res://maps/runtime_aliases.map", "hero_scene: res://scenes/hero.tscn");

        ctx.Bootstrap();
        ctx.FlushFrame();

        ctx.Template.LoadSceneAliases("res://maps/runtime_aliases.map");

        Assert.Equal(
            "res://scenes/hero.tscn",
            ctx.Runtime.SndWorld.Mappings.ResolveSceneAlias("hero_scene"));
    }

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateContext(
        string? templateMapPath,
        string? sceneAliasMapPath)
    {
        var logger = new TestLogger();
        var fs = new TestMemoryFileSystem();
        var host = new FullMemorySndSceneHost(logger);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var runtime = TestFactory.CreateRuntime(
            logger,
            host,
            new TypeStringMapping(),
            new Blackboard.Blackboard(),
            dataSourceIo);
        host.BindWorld(runtime.SndWorld);

        fs.SeedFile("res://entry/entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");

        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(
            runtime,
            dataSourceIo,
            metaAccess,
            pathResolver,
            "root",
            "res://initial",
            "res://entry/entry.json")
        {
            AutoDiscoverStrategies = false,
            SceneAliasMapPath = sceneAliasMapPath,
            SndTemplateMapPath = templateMapPath
        });
        host.BindContext(ctx);
        return (ctx, fs);
    }
}
