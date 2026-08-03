using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Console.CommandHandlers;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class InvokeStrategyCommandHandlerTests
{
    private const string _queryNameIndex = "invoke.cmd.query_name";
    private const string _cmdWithInputIndex = "invoke.cmd.with_input";

    [Fact]
    public void InvokeStrategy_NoInput_ReturnsResult()
    {
        var (runtime, host, output) = Setup();
        LoadEntities(host, [_queryNameIndex]);
        var handler = new InvokeStrategyCommandHandler(runtime);

        var ok = handler.TryExecute(CreateInvocation("E", _queryNameIndex),
            output, out var errorMessage);

        Assert.True(ok);
        Assert.Null(errorMessage);
        Assert.Contains(output.Entries, e => e.Contains('E'));
    }

    [Fact]
    public void InvokeStrategy_WithInput_PassesToStrategy()
    {
        var (runtime, host, output) = Setup();
        LoadEntities(host, [_cmdWithInputIndex]);
        var handler = new InvokeStrategyCommandHandler(runtime);

        var ok = handler.TryExecute(
            CreateInvocation("E", _cmdWithInputIndex, @"{""x"":64,""z"":64}"),
            output, out var errorMessage);

        Assert.True(ok);
        Assert.Null(errorMessage);
        Assert.Contains(output.Entries, e => e.Contains('x') && e.Contains('z'));
    }

    [Fact]
    public void InvokeStrategy_MissingEntity_OutputsError()
    {
        var (runtime, _, output) = Setup();
        var handler = new InvokeStrategyCommandHandler(runtime);

        var ok = handler.TryExecute(CreateInvocation("Ghost", _queryNameIndex),
            output, out var errorMessage);

        Assert.False(ok);
        Assert.Contains("Ghost", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void InvokeStrategy_NotActiveStrategy_OutputsError()
    {
        var (runtime, host, output) = Setup();
        LoadEntities(host, []);
        var handler = new InvokeStrategyCommandHandler(runtime);

        var ok = handler.TryExecute(CreateInvocation("E", "not.exist"),
            output, out var errorMessage);

        Assert.False(ok);
        Assert.Contains("not.exist", errorMessage, StringComparison.Ordinal);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static (OrigoRuntime runtime, ISndSceneHost host, CollectingConsoleOutputChannel output) Setup()
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new QueryNameStrategy());
        world.RegisterStrategy(() => new CmdWithInputStrategy());
        host.BindWorld(world);

        var fs = new TestMemoryFileSystem();
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        host.BindContext(ctx);
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var output = new CollectingConsoleOutputChannel();
        return (runtime, host, output);
    }

    private static void LoadEntities(ISndSceneHost host, string[] activeIndices)
    {
        host.RecoverFromMetaList(
        [
            new SndMetaData
            {
                Name = "E",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData
                {
                    LifecycleIndices = [],
                    ActiveIndices = [.. activeIndices]
                },
                DataMetaData = new DataMetaData()
            }
        ]);
    }

    private static CommandInvocation CreateInvocation(string entityName, string strategyIndex,
        string? input = null)
    {
        var args = input is not null
            ? new[] { entityName, strategyIndex, input }
            : [entityName, strategyIndex];
        return new CommandInvocation
        {
            Command = "invoke_strategy",
            PositionalArgs = args,
            NamedArgs = new Dictionary<string, string>()
        };
    }

    // ── Test strategies ────────────────────────────────────────────────

    [StrategyIndex(_queryNameIndex)]
    private sealed class QueryNameStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => entity.Name;
    }

    [StrategyIndex(_cmdWithInputIndex)]
    private sealed class CmdWithInputStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => $"received input: {input}";
    }

    // ── Test output channel ────────────────────────────────────────────

    private sealed class CollectingConsoleOutputChannel : IConsoleOutputChannel
    {
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries => _entries;

        public long Subscribe(Action<string> onLine) => 0;

        public bool Unsubscribe(long subscriptionId) => false;

        public void Publish(string line) => _entries.Add(line);
    }
}
