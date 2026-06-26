using System;
using System.Collections.Generic;
using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleTests
{
    [Fact]
    public void ConsoleCommandParser_Positional_SpawnMapsNameAndTemplate()
    {
        Assert.True(ConsoleCommandParser.TryParse("spawn myEntity myTpl", out var inv, out var err));
        Assert.Null(err);
        Assert.NotNull(inv);
        Assert.Equal("spawn", inv!.Command);
        Assert.Equal(2, inv.PositionalArgs.Count);
        Assert.Equal("myEntity", inv.PositionalArgs[0]);
        Assert.Equal("myTpl", inv.PositionalArgs[1]);
        Assert.Empty(inv.NamedArgs);
    }

    [Fact]
    public void ConsoleCommandParser_Named_SpawnMapsNameAndTemplate()
    {
        Assert.True(ConsoleCommandParser.TryParse("spawn name=e1 template=tpl_a", out var inv, out var err));
        Assert.Null(err);
        Assert.NotNull(inv);
        Assert.Equal("spawn", inv!.Command);
        Assert.Empty(inv.PositionalArgs);
        Assert.Equal("e1", inv.NamedArgs["name"]);
        Assert.Equal("tpl_a", inv.NamedArgs["template"]);
    }

    [Fact]
    public void OrigoConsole_SpawnTemplate_Positional_SpawnsWithResolvedName()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "enemy_template: templates/enemy.json");
        fs.SeedFile("templates/enemy.json",
            """
            {
              "name": "TemplateEnemy",
              "node": { "pairs": {} },
              "strategy": { "lifecycle_indices": [] },
              "data": { "pairs": {} }
            }
            """);

        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var typeMapping = new TypeStringMapping();

        var runtime = TestFactory.CreateRuntime(logger, sceneHost, typeMapping,
            new Blackboard.Blackboard(),
            new ConsoleInputBuffer(), new ConsoleOutputChannel(),
            TestFactory.CreateIoGateway(fs));

        runtime.SndWorld.LoadTemplates("maps/templates.map", logger);

        var input = runtime.ConsoleInput!;
        var output = (ConsoleOutputChannel)runtime.ConsoleOutputChannel!;
        var messages = new List<string>();
        output.Subscribe(messages.Add);

        input.Enqueue("spawn Boss1 enemy_template");
        runtime.Console!.ProcessPending();

        Assert.Single(sceneHost.BuildMetaList());
        Assert.Equal("Boss1", sceneHost.BuildMetaList()[0].Name);
        Assert.Contains(messages, m => m.Contains("Spawned 'Boss1'", StringComparison.Ordinal));
    }

    [Fact]
    public void OrigoConsole_SpawnTemplate_MissingTemplate_Throws()
    {
        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var typeMapping = new TypeStringMapping();

        var fs = new TestFileSystem();
        fs.SeedFile("maps/empty.map", "");

        var runtime = TestFactory.CreateRuntime(logger, sceneHost, typeMapping,
            new Blackboard.Blackboard(),
            new ConsoleInputBuffer(), new ConsoleOutputChannel(),
            TestFactory.CreateIoGateway(fs));

        runtime.SndWorld.LoadTemplates("maps/empty.map", logger);

        var input = runtime.ConsoleInput!;

        input.Enqueue("spawn X missing_tpl");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            runtime.Console!.ProcessPending());
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(sceneHost.BuildMetaList());
    }

    [Fact]
    public void OrigoConsole_SpawnTemplate_DuplicateName_ThrowsAndLeavesFirstSpawnIntact()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "enemy_template: templates/enemy.json");
        fs.SeedFile("templates/enemy.json",
            """
            {
              "name": "TemplateEnemy",
              "node": { "pairs": {} },
              "strategy": { "lifecycle_indices": [] },
              "data": { "pairs": {} }
            }
            """);

        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var typeMapping = new TypeStringMapping();

        var runtime = TestFactory.CreateRuntime(logger, sceneHost, typeMapping,
            new Blackboard.Blackboard(),
            new ConsoleInputBuffer(), new ConsoleOutputChannel(),
            TestFactory.CreateIoGateway(fs));

        runtime.SndWorld.LoadTemplates("maps/templates.map", logger);
        var input = runtime.ConsoleInput!;

        input.Enqueue("spawn Dup enemy_template");
        input.Enqueue("spawn Dup enemy_template");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            runtime.Console!.ProcessPending());
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Single(sceneHost.BuildMetaList());
    }
}
