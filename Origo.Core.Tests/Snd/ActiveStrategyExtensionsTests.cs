using System.Collections.Generic;
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

    private sealed class StubActiveStrategyEntity : Origo.Core.Abstractions.Entity.ISndEntity
    {
        private readonly System.Func<object?, object?> _invokeResult;

        public StubActiveStrategyEntity(System.Func<object?, object?> invokeResult)
        {
            _invokeResult = invokeResult;
        }

        public string Name => "stub";
        public bool IsPendingKill => false;

        public object? InvokeStrategy(string strategyIndex, object? input = null)
            => _invokeResult(input);

        public void SetData<T>(string name, T value) => throw new System.NotImplementedException();
        public T GetData<T>(string name) => throw new System.NotImplementedException();
        public (bool found, T? value) TryGetData<T>(string name) => throw new System.NotImplementedException();
        public void Subscribe(string name, System.Action<Origo.Core.Abstractions.Entity.ISndEntity,
            Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData> callback,
            System.Func<Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.ISndEntity,
                Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData, bool>? filter = null) => throw new System.NotImplementedException();
        public void Unsubscribe(string name, System.Action<Origo.Core.Abstractions.Entity.ISndEntity,
            Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData> callback) => throw new System.NotImplementedException();
        public Origo.Core.Abstractions.Node.INodeHandle GetNode(string name) => throw new System.NotImplementedException();
        public IReadOnlyCollection<string> GetNodeNames() => throw new System.NotImplementedException();
        public void AddStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveStrategy(string index) => throw new System.NotImplementedException();
        public void AddActiveStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveActiveStrategy(string index) => throw new System.NotImplementedException();
        public void SubscribeLifecycle(System.Action<Origo.Core.Abstractions.Entity.ISndEntity,
            Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.EntityLifecycleEvent> callback) => throw new System.NotImplementedException();
        public void UnsubscribeLifecycle(System.Action<Origo.Core.Abstractions.Entity.ISndEntity,
            Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.EntityLifecycleEvent> callback) => throw new System.NotImplementedException();
        public void ObserveData(Origo.Core.Abstractions.Entity.ISndEntity target, string dataName,
            System.Action<Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.ISndEntity,
                Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData> callback,
            System.Func<Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.ISndEntity,
                Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData, bool>? filter = null) => throw new System.NotImplementedException();
        public void UnobserveData(Origo.Core.Abstractions.Entity.ISndEntity target, string dataName,
            System.Action<Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.ISndEntity,
                Origo.Core.Snd.Metadata.TypedData, Origo.Core.Snd.Metadata.TypedData> callback) => throw new System.NotImplementedException();
        public void ObserveLifecycle(Origo.Core.Abstractions.Entity.ISndEntity target,
            System.Action<Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.ISndEntity,
                Origo.Core.Abstractions.Entity.EntityLifecycleEvent> callback) => throw new System.NotImplementedException();
        public void UnobserveLifecycle(Origo.Core.Abstractions.Entity.ISndEntity target,
            System.Action<Origo.Core.Abstractions.Entity.ISndEntity, Origo.Core.Abstractions.Entity.ISndEntity,
                Origo.Core.Abstractions.Entity.EntityLifecycleEvent> callback) => throw new System.NotImplementedException();
    }
}
