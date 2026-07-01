using System.Collections.Generic;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class ActiveStrategyExtensionsTests
{
    [Fact]
    public void InvokeStrategy_GenericWithInput_SerializesAndDeserializes()
    {
        var entity = new StubActiveStrategyEntity(o => "{ \"Result\": 42 }");
        var input = new { Sx = 1, Sz = 2 };

        var result = entity.InvokeStrategy<object, TestResult>(
            "test.find", input);

        Assert.NotNull(result);
        Assert.Equal(42, result.Result);
    }

    [Fact]
    public void InvokeStrategy_GenericNoInput_CallsWithoutInput()
    {
        var entity = new StubActiveStrategyEntity(o => "{ \"Result\": 99 }");

        var result = entity.InvokeStrategy<TestResult>("test.get");

        Assert.NotNull(result);
        Assert.Equal(99, result.Result);
    }

    [Fact]
    public void InvokeStrategy_NullResult_ReturnsDefault()
    {
        var entity = new StubActiveStrategyEntity(_ => null!);

        var result = entity.InvokeStrategy<object, TestResult>(
            "test.null", new { });

        Assert.Null(result);
    }

    private sealed class TestResult
    {
        public int Result { get; set; }
    }

    private sealed class StubActiveStrategyEntity(System.Func<object?, object?> invokeResult) : Origo.Core.Abstractions.Entity.ISndEntity
    {
        public ISessionRun OwningSession { get; set; } = null!;
        private readonly System.Func<object?, object?> _invokeResult = invokeResult;

        public string Name => "stub";
        public bool IsPendingKill => false;

        public object? InvokeStrategy(string strategyIndex, object? input = null)
            => _invokeResult(input);

        public void SetData<T>(string name, T value) => throw new System.NotImplementedException();
        public T GetData<T>(string name) => throw new System.NotImplementedException();
        public (bool found, T? value) TryGetData<T>(string name) => throw new System.NotImplementedException();
        public void MountObserverStrategy(string targetName, string observerIndex) { }

        public void UnmountObserverStrategy(string targetName, string observerIndex) { }
        public void MountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
        public void UnmountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
        public Origo.Core.Abstractions.Node.INodeHandle GetNode(string name) => throw new System.NotImplementedException();
        public IReadOnlyCollection<string> GetNodeNames() => throw new System.NotImplementedException();
        public void AddStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveStrategy(string index) => throw new System.NotImplementedException();
        public void AddActiveStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveActiveStrategy(string index) => throw new System.NotImplementedException();

    }
}
