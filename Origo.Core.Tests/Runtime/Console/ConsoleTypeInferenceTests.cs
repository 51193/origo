using System;
using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Runtime.Console.CommandHandlers;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleTypeInferenceTests
{
    // ── bb_set type inference ──────────────────────────────────────

    [Fact]
    public void BlackboardSet_IntLiteral_StoredAsInt32()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new BlackboardSetCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("bb_set", "system", "score", "42"), output, out var err);

        Assert.Null(err);
        var (found, value) = runtime.SystemBlackboard.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void BlackboardSet_NegativeInt_StoredAsInt32()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new BlackboardSetCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("bb_set", "system", "neg", "-5"), output, out var err);

        Assert.Null(err);
        var (found, value) = runtime.SystemBlackboard.TryGet<int>("neg");
        Assert.True(found);
        Assert.Equal(-5, value);
    }

    [Fact]
    public void BlackboardSet_FloatLiteral_StoredAsSingle()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new BlackboardSetCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("bb_set", "system", "pi", "3.14"), output, out var err);

        Assert.Null(err);
        var (found, value) = runtime.SystemBlackboard.TryGet<float>("pi");
        Assert.True(found);
        Assert.Equal(3.14f, value);
    }

    [Fact]
    public void BlackboardSet_TrueLiteral_StoredAsBoolean()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new BlackboardSetCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("bb_set", "system", "flag", "true"), output, out var err);

        Assert.Null(err);
        var (found, value) = runtime.SystemBlackboard.TryGet<bool>("flag");
        Assert.True(found);
        Assert.True(value);
    }

    [Fact]
    public void BlackboardSet_FalseLiteral_StoredAsBoolean()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new BlackboardSetCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("bb_set", "system", "flag2", "false"), output, out var err);

        Assert.Null(err);
        var (found, value) = runtime.SystemBlackboard.TryGet<bool>("flag2");
        Assert.True(found);
        Assert.False(value);
    }

    [Fact]
    public void BlackboardSet_NonNumericLiteral_StoredAsString()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new BlackboardSetCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("bb_set", "system", "msg", "hello_world"), output, out var err);

        Assert.Null(err);
        var (found, value) = runtime.SystemBlackboard.TryGet<string>("msg");
        Assert.True(found);
        Assert.Equal("hello_world", value);
    }

    [Fact]
    public void BlackboardSet_UnknownLayer_ReturnsError()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new BlackboardSetCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(
            CreateInvocation("bb_set", "unknown", "key", "42"), output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("layer", err, StringComparison.OrdinalIgnoreCase);
    }

    // ── entity_set_data type inference ─────────────────────────────

    [Fact]
    public void EntitySetData_NewKey_IntLiteral_StoredAsInt32()
    {
        var runtime = CreateRuntimeWithConsoleAndEntity("player");
        var handler = new SetEntityDataCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("entity_set_data", "player", "hp", "100"), output, out var err);

        Assert.Null(err);
        var entity = runtime.ForegroundSceneHost.FindByName("player");
        Assert.NotNull(entity);
        var (found, value) = entity!.TryGetData<int>("hp");
        Assert.True(found);
        Assert.Equal(100, value);
    }

    [Fact]
    public void EntitySetData_NewKey_FloatLiteral_StoredAsSingle()
    {
        var runtime = CreateRuntimeWithConsoleAndEntity("player");
        var handler = new SetEntityDataCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("entity_set_data", "player", "speed", "1.5"), output, out var err);

        Assert.Null(err);
        var entity = runtime.ForegroundSceneHost.FindByName("player");
        Assert.NotNull(entity);
        var (found, value) = entity!.TryGetData<float>("speed");
        Assert.True(found);
        Assert.Equal(1.5f, value);
    }

    [Fact]
    public void EntitySetData_NewKey_BoolLiteral_StoredAsBoolean()
    {
        var runtime = CreateRuntimeWithConsoleAndEntity("player");
        var handler = new SetEntityDataCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("entity_set_data", "player", "alive", "true"), output, out var err);

        Assert.Null(err);
        var entity = runtime.ForegroundSceneHost.FindByName("player");
        Assert.NotNull(entity);
        var (found, value) = entity!.TryGetData<bool>("alive");
        Assert.True(found);
        Assert.True(value);
    }

    [Fact]
    public void EntitySetData_NewKey_StringLiteral_StoredAsString()
    {
        var runtime = CreateRuntimeWithConsoleAndEntity("player");
        var handler = new SetEntityDataCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("entity_set_data", "player", "tag", "hero"), output, out var err);

        Assert.Null(err);
        var entity = runtime.ForegroundSceneHost.FindByName("player");
        Assert.NotNull(entity);
        var (found, value) = entity!.TryGetData<string>("tag");
        Assert.True(found);
        Assert.Equal("hero", value);
    }

    [Fact]
    public void EntitySetData_ExistingKey_PreservesType()
    {
        var runtime = CreateRuntimeWithConsoleAndEntity("player");
        var entity = runtime.ForegroundSceneHost.FindByName("player")!;
        entity.SetData("hunger", 50.0f);

        var handler = new SetEntityDataCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        handler.TryExecute(
            CreateInvocation("entity_set_data", "player", "hunger", "15"), output, out var err);

        Assert.Null(err);
        var (found, value) = entity.TryGetData<float>("hunger");
        Assert.True(found);
        Assert.Equal(15.0f, value);
    }

    [Fact]
    public void EntitySetData_EntityNotFound_ReturnsError()
    {
        var runtime = CreateRuntimeWithConsole();
        var handler = new SetEntityDataCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(
            CreateInvocation("entity_set_data", "nonexistent", "hp", "50"), output, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("not found", err, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static OrigoRuntime CreateRuntimeWithConsole()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var systemBb = new Blackboard.Blackboard();
        var input = new ConsoleInputBuffer();
        var output = new ConsoleOutputChannel();

        return TestFactory.CreateRuntime(logger, host, tm, systemBb, input, output);
    }

    private static OrigoRuntime CreateRuntimeWithConsoleAndEntity(string entityName)
    {
        var runtime = CreateRuntimeWithConsole();
        runtime.ForegroundSceneHost.CreateEntity(new SndMetaData
        {
            Name = entityName,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        return runtime;
    }

    private static CommandInvocation CreateInvocation(string command, params string[] positionalArgs)
    {
        return new CommandInvocation
        {
            Command = command,
            PositionalArgs = positionalArgs,
            NamedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }
}
