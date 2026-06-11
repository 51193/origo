using System;
using System.Collections.Generic;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class JsonAndMappingsTests
{
    private const string StrategyMove = "test.move";
    private const string StrategyAttack = "test.attack";
    private const string StrategyAi = "test.ai";
    private const string StrategyTalk = "test.talk";

    [Fact]
    public void SndMetaData_RoundTripPreservesTypedData()
    {
        var typeMapping = new TypeStringMapping();
        var codec = TestFactory.CreateJsonCodec();
        var registry = TestFactory.CreateRegistry(typeMapping);
        var meta = new SndMetaData
        {
            Name = "Hero",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["body"] = "hero_prefab" } },
            StrategyMetaData = new StrategyMetaData
                { EntityIndices = new List<string> { StrategyMove, StrategyAttack } },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["hp"] = new(typeof(int), 100),
                    ["title"] = new(typeof(string), "Knight")
                }
            }
        };

        var node = registry.Write(meta);
        var json = codec.Encode(node);
        var parsedNode = codec.Decode(json);
        var parsed = registry.Read<SndMetaData>(parsedNode);

        Assert.Equal("Hero", parsed.Name);
        Assert.Equal("hero_prefab", parsed.NodeMetaData!.Pairs["body"]);
        Assert.Equal(new[] { StrategyMove, StrategyAttack }, parsed.StrategyMetaData!.EntityIndices);
        Assert.Equal(100, Assert.IsType<int>(parsed.DataMetaData!.Pairs["hp"].Data));
        Assert.Equal("Knight", Assert.IsType<string>(parsed.DataMetaData.Pairs["title"].Data));
    }

    [Fact]
    public void SndMappings_LoadSceneAliases_DuplicateKey_LastWins()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/dup_scenes.map", "hero: res://first.tscn\nhero: res://second.tscn\n");
        var io = TestFactory.CreateIoGateway(fs);
        var mappings = new SndMappings();
        var logger = new TestLogger();
        mappings.LoadSceneAliases(io, "maps/dup_scenes.map", logger);

        Assert.Equal("res://second.tscn", mappings.ResolveSceneAlias("hero"));
    }

    [Fact]
    public void SndMappings_LoadSceneAliasesAndTemplates_ResolveExpectedValues()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/scenes.map", "# comment\nhero: res://hero.tscn\nui: res://ui/menu.tscn");
        fs.SeedFile("maps/templates.map", "hero_template: templates/hero.json");
        fs.SeedFile("templates/hero.json",
            """
            {
              "name": "TemplateHero",
              "node": { "pairs": { "root": "hero" } },
              "strategy": { "entity_indices": [ "test.move" ] },
              "data": { "pairs": { "hp": { "type": "Int32", "data": 150 } } }
            }
            """);

        var mappings = new SndMappings();
        var logger = new TestLogger();
        var io = TestFactory.CreateIoGateway(fs);
        var registry = TestFactory.CreateRegistry(new TypeStringMapping());

        mappings.LoadSceneAliases(io, "maps/scenes.map", logger);
        mappings.LoadTemplates(io, "maps/templates.map", registry, logger);

        Assert.Equal("res://hero.tscn", mappings.ResolveSceneAlias("hero"));
        Assert.Throws<KeyNotFoundException>(() => mappings.ResolveSceneAlias("missing_alias"));

        var template = mappings.ResolveTemplate("hero_template");
        Assert.Equal("TemplateHero", template.Name);
        Assert.Equal(150, Assert.IsType<int>(template.DataMetaData!.Pairs["hp"].Data));

        var readsAfterFirstResolve = fs.ReadAllTextCallCount;
        _ = mappings.ResolveTemplate("hero_template");
        Assert.Equal(readsAfterFirstResolve, fs.ReadAllTextCallCount);
    }

    [Fact]
    public void SndMappings_ResolveMetaListFromJsonArray_SupportsTemplateAndInlineMix()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "enemy_template: templates/enemy.json");
        fs.SeedFile("templates/enemy.json",
            """
            {
              "name": "TemplateEnemy",
              "node": { "pairs": { "root": "enemy" } },
              "strategy": { "entity_indices": [ "test.ai" ] },
              "data": { "pairs": { "damage": { "type": "Int32", "data": 8 } } }
            }
            """);
        var codec = TestFactory.CreateJsonCodec();
        var io = TestFactory.CreateIoGateway(fs);
        var registry = TestFactory.CreateRegistry(new TypeStringMapping());
        var mappings = new SndMappings();
        mappings.LoadTemplates(io, "maps/templates.map", registry, NullLogger.Instance);

        var json = """
                   [
                     { "sndName": "EnemyA", "templateKey": "enemy_template" },
                     {
                       "name": "Npc",
                       "node": { "pairs": { "root": "npc" } },
                       "strategy": { "entity_indices": [ "test.talk" ] },
                       "data": { "pairs": { "mood": { "type": "String", "data": "Calm" } } }
                     }
                   ]
                   """;

        var node = codec.Decode(json);
        var metas = mappings.ResolveMetaListFromJsonArray(node, registry);

        Assert.Equal(2, metas.Count);
        Assert.Equal("EnemyA", metas[0].Name);
        Assert.Equal(8, Assert.IsType<int>(metas[0].DataMetaData!.Pairs["damage"].Data));
        Assert.Equal("Npc", metas[1].Name);
        Assert.Equal("Calm", Assert.IsType<string>(metas[1].DataMetaData!.Pairs["mood"].Data));
    }

    [Fact]
    public void TypedDataJson_DataPropertyBeforeType_DeserializesCorrectly()
    {
        var codec = TestFactory.CreateJsonCodec();
        var registry = TestFactory.CreateRegistry(new TypeStringMapping());
        const string json = """{"data":42,"type":"Int32"}""";

        var node = codec.Decode(json);
        var td = registry.Read<TypedData>(node);
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, Assert.IsType<int>(td.Data));
    }

    [Fact]
    public void Blackboard_SerializeAll_ReturnsDetachedCopy()
    {
        var bb = new Blackboard.Blackboard();
        bb.SetValue("k", 1);

        var exported = bb.SerializeAll();
        Assert.Single(exported);
        Assert.Equal(1, Assert.IsType<int>(exported["k"].Data));

        ((Dictionary<string, TypedData>)exported).Clear();
        Assert.Single(bb.GetKeys());
        var (foundK, kVal) = bb.TryGet<int>("k");
        Assert.True(foundK);
        Assert.Equal(1, kVal);
    }

    [Fact]
    public void SndMappings_ResolveTemplate_BeforeLoadTemplates_Throws()
    {
        var mappings = new SndMappings();
        Assert.Throws<InvalidOperationException>(() => mappings.ResolveTemplate("any"));
    }

    [Fact]
    public void SndMappings_ResolveTemplate_AfterLoadTemplatesWithEmptyMap_Throws()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/empty_templates.map", "# no entries\n");
        var io = TestFactory.CreateIoGateway(fs);
        var registry = TestFactory.CreateRegistry(new TypeStringMapping());
        var mappings = new SndMappings();
        mappings.LoadTemplates(io, "maps/empty_templates.map", registry, NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(() => mappings.ResolveTemplate("any_alias"));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SndMappings_ResolveTemplate_InvalidJson_Throws()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "bad_template: templates/bad.json");
        fs.SeedFile("templates/bad.json", "{ invalid-json");
        var io = TestFactory.CreateIoGateway(fs);
        var registry = TestFactory.CreateRegistry(new TypeStringMapping());
        var mappings = new SndMappings();
        var logger = new TestLogger();
        mappings.LoadTemplates(io, "maps/templates.map", registry, logger);

        Assert.ThrowsAny<Exception>(() => mappings.ResolveTemplate("bad_template"));
    }
}
