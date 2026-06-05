using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Console;
using Origo.GodotAdapter.Tests.TestSupport;
using Xunit;

namespace Origo.GodotAdapter.Tests.Console;

public class PressButtonCommandHandlerTests
{
    private static CommandInvocation MakeInvocation(string entityName, string buttonPath)
    {
        return new CommandInvocation
        {
            Command = "press_button",
            PositionalArgs = new[] { entityName, buttonPath },
            NamedArgs = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void Properties_HaveExpectedValues()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new PressButtonCommandHandler(runtime);

        Assert.Equal("press_button", handler.Name);
        Assert.Contains("<entity>", handler.HelpText);
        Assert.Contains("<path>", handler.HelpText);
        Assert.Equal(2, handler.MinPositionalArgs);
        Assert.Equal(2, handler.MaxPositionalArgs);
    }

    [Fact]
    public void TryExecute_TooFewArgs_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new PressButtonCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var invocation = new CommandInvocation
        {
            Command = "press_button",
            PositionalArgs = new[] { "Entity" },
            NamedArgs = new Dictionary<string, string>()
        };

        var ok = handler.TryExecute(invocation, output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("参数数量不合法", error);
    }

    [Fact]
    public void TryExecute_EntityNotFound_ReturnsError()
    {
        var (runtime, _) = TestRuntimeHelper.CreateRuntime();
        var handler = new PressButtonCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(
            MakeInvocation("NonExistent", "PlayButton"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("Entity 'NonExistent' not found", error);
    }

    [Fact]
    public void TryExecute_EntityNotGodot_ReturnsError()
    {
        var (runtime, sceneHost) = TestRuntimeHelper.CreateRuntime();
        sceneHost.AddEntity(new DummyEntity("DummyEntity"));
        var handler = new PressButtonCommandHandler(runtime);
        var output = new ConsoleOutputChannel();

        var ok = handler.TryExecute(
            MakeInvocation("DummyEntity", "PlayButton"), output, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("is not a Godot entity", error);
    }

    private sealed class DummyEntity : ISndEntity
    {
        public DummyEntity(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public void SetData<T>(string name, T value)
        {
        }

        public T GetData<T>(string name) => default!;
        public (bool found, T? value) TryGetData<T>(string name) => (false, default);

        public void Subscribe(string name, Action<ISndEntity, ISndEntity, object?, object?> callback,
            Func<ISndEntity, ISndEntity, object?, object?, bool>? filter = null)
        {
        }

        public void Unsubscribe(string name, Action<ISndEntity, ISndEntity, object?, object?> callback)
        {
        }

        public void SubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public void UnsubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public void ObserveData(ISndEntity target, string dataName,
            Action<ISndEntity, ISndEntity, object?, object?> callback,
            Func<ISndEntity, ISndEntity, object?, object?, bool>? filter = null)
        {
        }

        public void UnobserveData(ISndEntity target, string dataName,
            Action<ISndEntity, ISndEntity, object?, object?> callback)
        {
        }

        public void ObserveLifecycle(ISndEntity target,
            Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public void UnobserveLifecycle(ISndEntity target,
            Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback)
        {
        }

        public INodeHandle GetNode(string name) =>
            throw new InvalidOperationException($"Node '{name}' not found.");

        public IReadOnlyCollection<string> GetNodeNames() => Array.Empty<string>();

        public void AddStrategy(string index)
        {
        }

        public void RemoveStrategy(string index)
        {
        }

        public void AddActiveStrategy(string index)
        {
        }

        public void RemoveActiveStrategy(string index)
        {
        }

        public object? InvokeStrategy(string strategyIndex, object? input = null) => null;

        public bool IsPendingKill { get; set; }
    }
}
