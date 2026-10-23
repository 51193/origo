using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Console.CommandHandlers;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class SpawnTemplateCommandHandlerTests
{
    [Fact]
    public void SpawnTemplateCommandHandler_MixNamedAndPositional_ReturnsError()
    {
        var runtime = TestFactory.CreateRuntime();
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = ["extraPositional"],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "n",
                ["template"] = "t"
            }
        };
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("mix", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpawnTemplateCommandHandler_NamedMissingName_ReturnsError()
    {
        var runtime = TestFactory.CreateRuntime();
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = "t"
            }
        };
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.Contains("name", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpawnTemplateCommandHandler_UnknownTemplate_ReturnsErrorInsteadOfThrowing()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("maps/templates.map", "hero_template: templates/hero.json");
        fs.SeedFile("templates/hero.json", """{"name":"TemplateHero","node":{"pairs":{"root":"hero"}}}""");
        var runtime = TestFactory.CreateRuntime(
            new TestLogger(), new TestSndSceneHost(), new TypeStringMapping(), new Blackboard.Blackboard(), fs);
        runtime.SndWorld.LoadTemplates("maps/templates.map", new TestLogger());
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "hero",
                ["template"] = "missing_template"
            }
        };
        var output = new ConsoleOutputChannel();

        // A mistyped template alias is a user input error, not a bug signal:
        // it must surface as a command error (like bb_get's unknown layer)
        // instead of throwing through the frame loop.
        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("missing_template", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpawnTemplateCommandHandler_DuplicateEntityName_ReturnsErrorInsteadOfThrowing()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("maps/templates.map", "hero_template: templates/hero.json");
        fs.SeedFile("templates/hero.json", """{"name":"TemplateHero","node":{"pairs":{"root":"hero"}}}""");
        fs.SeedFile("entry.json", """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(
            logger, new TestSndSceneHost(), new TypeStringMapping(), new Blackboard.Blackboard(), fs);
        runtime.SndWorld.LoadTemplates("maps/templates.map", logger);
        var context = new SndContext(new SndContextParameters(
            runtime,
            TestFactory.CreateIoGateway(fs),
            TestFactory.CreateFileMetaAccess(fs),
            TestFactory.CreatePathResolver(fs),
            "root",
            "initial",
            "entry.json"));
        context.Lifecycle.RequestLoadMainMenuEntrySave();
        context.FlushFrame();
        var session = runtime.SessionManager.ForegroundSession!;
        session.Spawn(new SndMetaData { Name = "hero" });

        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "hero",
                ["template"] = "hero_template"
            }
        };
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("already exists", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FrameLoop_DuplicateSpawnDoesNotBlockFollowingCommand()
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("maps/templates.map", "hero_template: templates/hero.json");
        fs.SeedFile("templates/hero.json", """{"name":"TemplateHero","node":{"pairs":{"root":"hero"}}}""");
        fs.SeedFile("entry.json", """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(
            logger,
            new TestSndSceneHost(),
            new TypeStringMapping(),
            new Blackboard.Blackboard(),
            input,
            output,
            TestFactory.CreateIoGateway(fs));
        runtime.SndWorld.LoadTemplates("maps/templates.map", logger);
        var context = new SndContext(new SndContextParameters(
            runtime,
            TestFactory.CreateIoGateway(fs),
            TestFactory.CreateFileMetaAccess(fs),
            TestFactory.CreatePathResolver(fs),
            "root",
            "initial",
            "entry.json"));
        context.Lifecycle.RequestLoadMainMenuEntrySave();
        context.FlushFrame();
        runtime.SessionManager.ForegroundSession!.Spawn(new SndMetaData { Name = "hero" });

        var lines = new List<string>();
        output.Subscribe(lines.Add);
        input.Enqueue("spawn name=hero template=hero_template");
        input.Enqueue("bb_set system after_duplicate 1");

        var ex = Record.Exception(() => ((IOrigoFrameDriver)runtime).DriveFrame(0.016));

        Assert.Null(ex);
        Assert.Contains(lines, line => line.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        var (found, value) = runtime.SystemBlackboard.TryGet<int>("after_duplicate");
        Assert.True(found);
        Assert.Equal(1, value);
    }

    [Fact]
    public void SpawnTemplateCommandHandler_TemplatesNotLoaded_ReturnsErrorInsteadOfThrowing()
    {
        var runtime = TestFactory.CreateRuntime();
        var handler = new SpawnTemplateCommandHandler(runtime);
        var invocation = new CommandInvocation
        {
            Command = "spawn",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "hero",
                ["template"] = "hero_template"
            }
        };
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(invocation, output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("template", err, StringComparison.OrdinalIgnoreCase);
    }
}
