using System.Collections.Generic;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class ActiveStrategyJsonBaseTests
{
    [Fact]
    public void Invoke_ValidJsonInput_DeserializesAndSerializesResult()
    {
        var strategy = new EchoStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, "{\"Value\": 7}");

        var json = Assert.IsType<string>(result);
        var payload = System.Text.Json.JsonSerializer.Deserialize<TestPayload>(json);
        Assert.NotNull(payload);
        Assert.Equal(7, payload!.Value);
    }

    [Fact]
    public void Invoke_StringResult_IsSerializedAsJsonString()
    {
        var strategy = new StringResultStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, "{}");

        var json = Assert.IsType<string>(result);
        Assert.Equal("\"ok\"", json);
    }

    [Fact]
    public void Invoke_ErrorResult_IsSerializedAsJsonString()
    {
        var strategy = new StringResultStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, "{\"Value\": -1}");

        var json = Assert.IsType<string>(result);
        Assert.Equal("\"err:invalid\"", json);
    }

    [Fact]
    public void Invoke_InvalidJsonInput_ReturnsErrorResult()
    {
        var strategy = new EchoStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, "not-json");

        var json = Assert.IsType<string>(result);
        Assert.StartsWith("\"err:", json);
    }

    [Fact]
    public void Invoke_NonStringInput_ReturnsErrorResult()
    {
        var strategy = new EchoStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, new object());

        var json = Assert.IsType<string>(result);
        Assert.StartsWith("\"err:", json);
    }

    [Fact]
    public void Invoke_NullResult_SerializesNull()
    {
        var strategy = new NullResultStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, "{}");

        Assert.Equal("null", result);
    }

    [Fact]
    public void GenericInvoke_JsonBaseStrategy_RoundTripsThroughExtensions()
    {
        var strategy = new EchoStrategy();
        var entity = new InvokeThroughStubEntity(input => strategy.Invoke(null!, NullSndContext.Instance, input));

        var result = entity.InvokeStrategy<TestPayload, TestPayload>(
            "test.echo", new TestPayload { Value = 3 });

        Assert.NotNull(result);
        Assert.Equal(3, result!.Value);
    }

    [Fact]
    public void GenericInvoke_BareStringResult_ReturnsStringAsIs()
    {
        // Legacy strategies returning bare strings must not throw opaque
        // JsonExceptions at the call site.
        var entity = new InvokeThroughStubEntity(_ => "ok");

        var result = entity.InvokeStrategy<string>("test.bare");

        Assert.Equal("ok", result);
    }

    [Fact]
    public void GenericInvoke_ErrorBareString_ReturnsStringAsIs()
    {
        var entity = new InvokeThroughStubEntity(_ => "err:no gold");

        var result = entity.InvokeStrategy<string>("test.bare");

        Assert.Equal("err:no gold", result);
    }


    [Fact]
    public void Invoke_NullInput_ExecutesWithDefault()
    {
        var strategy = new IntResultStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, null);

        var json = Assert.IsType<string>(result);
        Assert.Equal("0", json);
    }

    [Fact]
    public void Invoke_StringReferenceTypeInput_RoundTrips()
    {
        var strategy = new EchoStringStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, "\"hello\"");

        var json = Assert.IsType<string>(result);
        Assert.Equal("\"hello\"", json);
    }

    [Fact]
    public void Invoke_NullJsonInput_ExecutesWithNullReference()
    {
        var strategy = new EchoStringStrategy();

        var result = strategy.Invoke(
            null!, NullSndContext.Instance, "null");

        Assert.Equal("null", result);
    }
    private sealed class TestPayload
    {
        public int Value { get; set; }
    }

    private sealed class EchoStrategy : ActiveStrategyJsonBase<TestPayload>
    {
        protected override object? Execute(
            Origo.Core.Abstractions.Entity.ISndEntity entity, ISndContext ctx, TestPayload? input) => input;
    }

    private sealed class StringResultStrategy : ActiveStrategyJsonBase<TestPayload>
    {
        protected override object? Execute(
            Origo.Core.Abstractions.Entity.ISndEntity entity, ISndContext ctx, TestPayload? input)
        {
            return input is { Value: >= 0 }
                ? ActiveStrategyResults.Ok()
                : ActiveStrategyResults.Err("invalid");
        }
    }

    private sealed class NullResultStrategy : ActiveStrategyJsonBase<TestPayload>
    {
        protected override object? Execute(
            Origo.Core.Abstractions.Entity.ISndEntity entity, ISndContext ctx, TestPayload? input) => null;
    }


    private sealed class IntResultStrategy : ActiveStrategyJsonBase<int>
    {
        protected override object? Execute(
            Origo.Core.Abstractions.Entity.ISndEntity entity, ISndContext ctx, int input) => input;
    }

    private sealed class EchoStringStrategy : ActiveStrategyJsonBase<string>
    {
        protected override object? Execute(
            Origo.Core.Abstractions.Entity.ISndEntity entity, ISndContext ctx, string input) => input;
    }

    private sealed class InvokeThroughStubEntity(System.Func<object?, object?> invokeResult)
        : Origo.Core.Abstractions.Entity.ISndEntity
    {
        public Origo.Core.Abstractions.Lifecycle.ISessionRun OwningSession => throw new System.NotImplementedException();
        private readonly System.Func<object?, object?> _invokeResult = invokeResult;

        public string Name => "stub";
        public bool IsPendingKill => false;

        public object? InvokeStrategy(string strategyIndex, object? input = null) => _invokeResult(input);

        public void SetData<T>(string name, T value) => throw new System.NotImplementedException();
        public T GetData<T>(string name) where T : notnull => throw new System.NotImplementedException();
        public (bool found, T? value) TryGetData<T>(string name) => throw new System.NotImplementedException();
        public void MountObserverStrategy(string targetName, string observerIndex) { }

        public bool TryGetData<T>(string name, out T? value)
        {
            var (found, stored) = TryGetData<T>(name);
            value = stored;
            return found;
        }

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
