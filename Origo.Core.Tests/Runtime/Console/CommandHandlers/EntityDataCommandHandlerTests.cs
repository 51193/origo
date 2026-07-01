using System.Collections.Generic;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class EntityDataCommandHandlerTests
{
    private static (
        OrigoRuntime runtime,
        TestSndSceneHost sceneHost,
        ConsoleInputBuffer input,
        ConsoleOutputChannel output,
        List<string> messages) CreateConsoleRuntime()
    {
        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();
        var consoleInput = new ConsoleInputBuffer();
        var consoleOutput = new ConsoleOutputChannel();

        var runtime = TestFactory.CreateRuntime(logger, sceneHost, tm, bb, consoleInput, consoleOutput);

        TestFactory.BootstrapForegroundSession(runtime);

        var messages = new List<string>();
        consoleOutput.Subscribe(messages.Add);

        return (runtime, sceneHost, consoleInput, consoleOutput, messages);
    }

    private static DummySndEntity CreateEntity(TestSndSceneHost host, string name) =>
        (DummySndEntity)host.CreateEntity(new SndMetaData { Name = name });

    // ── entity_set_data ──

    [Fact]
    public void EntitySetData_EntityNotFound_ReportsError()
    {
        var (runtime, _, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("entity_set_data missing key 42");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("not found", messages[0]);
    }

    [Fact]
    public void EntitySetData_IntValue_StoresCorrectly()
    {
        var (runtime, sceneHost, input, _, messages) = CreateConsoleRuntime();
        CreateEntity(sceneHost, "player");

        input.Enqueue("entity_set_data player hp 100");
        runtime.Console!.ProcessPending();

        var entity = (DummySndEntity)sceneHost.FindByName("player")!;
        Assert.Equal(100, entity.GetData<int>("hp"));
        Assert.Contains("[player] hp = 100", messages[0]);
    }

    [Fact]
    public void EntitySetData_FloatValue_StoresCorrectly()
    {
        var (runtime, sceneHost, input, _, messages) = CreateConsoleRuntime();
        CreateEntity(sceneHost, "player");

        input.Enqueue("entity_set_data player speed 3.14");
        runtime.Console!.ProcessPending();

        var entity = (DummySndEntity)sceneHost.FindByName("player")!;
        Assert.Equal(3.14f, entity.GetData<float>("speed"));
        Assert.Contains("[player] speed = 3.14", messages[0]);
    }

    [Fact]
    public void EntitySetData_BoolValue_StoresCorrectly()
    {
        var (runtime, sceneHost, input, _, messages) = CreateConsoleRuntime();
        CreateEntity(sceneHost, "player");

        input.Enqueue("entity_set_data player alive true");
        runtime.Console!.ProcessPending();

        var entity = (DummySndEntity)sceneHost.FindByName("player")!;
        Assert.True(entity.GetData<bool>("alive"));
        Assert.Contains("[player] alive = true", messages[0]);
    }

    [Fact]
    public void EntitySetData_StringValue_StoresCorrectly()
    {
        var (runtime, sceneHost, input, _, messages) = CreateConsoleRuntime();
        CreateEntity(sceneHost, "player");

        input.Enqueue("entity_set_data player name Alice");
        runtime.Console!.ProcessPending();

        var entity = (DummySndEntity)sceneHost.FindByName("player")!;
        Assert.Equal("Alice", entity.GetData<string>("name"));
        Assert.Contains("[player] name = Alice", messages[0]);
    }

    [Fact]
    public void EntitySetData_PreservesExistingIntType()
    {
        var (runtime, sceneHost, input, _, _) = CreateConsoleRuntime();
        var entity = CreateEntity(sceneHost, "player");
        entity.SetData("hp", 50);

        input.Enqueue("entity_set_data player hp 99");
        runtime.Console!.ProcessPending();

        Assert.Equal(99, entity.GetData<int>("hp"));
    }

    [Fact]
    public void EntitySetData_PreservesExistingFloatType()
    {
        var (runtime, sceneHost, input, _, _) = CreateConsoleRuntime();
        var entity = CreateEntity(sceneHost, "player");
        entity.SetData("speed", 1.5f);

        input.Enqueue("entity_set_data player speed 2.5");
        runtime.Console!.ProcessPending();

        Assert.Equal(2.5f, entity.GetData<float>("speed"));
    }

    [Fact]
    public void EntitySetData_PreservesExistingBoolType()
    {
        var (runtime, sceneHost, input, _, _) = CreateConsoleRuntime();
        var entity = CreateEntity(sceneHost, "player");
        entity.SetData("alive", false);

        input.Enqueue("entity_set_data player alive true");
        runtime.Console!.ProcessPending();

        Assert.True(entity.GetData<bool>("alive"));
    }

    [Fact]
    public void EntitySetData_PreservesExistingStringType()
    {
        var (runtime, sceneHost, input, _, _) = CreateConsoleRuntime();
        var entity = CreateEntity(sceneHost, "player");
        entity.SetData("name", "OldName");

        input.Enqueue("entity_set_data player name NewName");
        runtime.Console!.ProcessPending();

        Assert.Equal("NewName", entity.GetData<string>("name"));
    }

    // ── entity_get_data ──

    [Fact]
    public void EntityGetData_EntityNotFound_ReportsError()
    {
        var (runtime, _, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("entity_get_data missing hp");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("not found", messages[0]);
    }

    [Fact]
    public void EntityGetData_Found_ReportsValueAndType()
    {
        var (runtime, sceneHost, input, _, messages) = CreateConsoleRuntime();
        var entity = CreateEntity(sceneHost, "player");
        entity.SetData("hp", 42);

        input.Enqueue("entity_get_data player hp");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("hp = 42", messages[0]);
        Assert.Contains("Int32", messages[0]);
    }

    [Fact]
    public void EntityGetData_NotFound_ReportsNotFound()
    {
        var (runtime, sceneHost, input, _, messages) = CreateConsoleRuntime();
        CreateEntity(sceneHost, "player");

        input.Enqueue("entity_get_data player missing_key");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("not found on entity", messages[0]);
    }

    [Fact]
    public void EntityGetData_MissingArgs_ReportsUsage()
    {
        var (runtime, _, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("entity_get_data");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("参数数量不合法", messages[0]);
    }

    [Fact]
    public void EntitySetData_MissingArgs_ReportsUsage()
    {
        var (runtime, _, input, _, messages) = CreateConsoleRuntime();

        input.Enqueue("entity_set_data player");
        runtime.Console!.ProcessPending();

        Assert.Single(messages);
        Assert.Contains("参数数量不合法", messages[0]);
    }
}
