using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class SndEntityNodeExtensionsIntegrationTests
{
    [IntegrationTest(Description = "GetNativeNode on non-GodotNodeHandle returns null")]
    public void GetNativeNode_NonGodotHandle_ReturnsNull()
    {
        var stubHandle = new StubNodeHandle("test");
        var result = stubHandle.GetNativeNode();
        IntegrationTestRunner.AssertNull(result, "result");
    }

    [IntegrationTest(Description = "GetNativeNode on GodotNodeHandle returns the underlying Node")]
    public void GetNativeNode_GodotHandle_ReturnsNode()
    {
        var node = new Node
        {
            Name = "test_node"
        };
        var handle = new GodotNodeHandle(node);

        var result = handle.GetNativeNode();
        IntegrationTestRunner.AssertNotNull(result, "result");
        IntegrationTestRunner.Assert(ReferenceEquals(node, result), "Should return the exact same Node reference.");

        node.Free();
    }

    [IntegrationTest(Description = "GetNodeFromSnd on non-GodotSndEntity returns null")]
    public void GetNodeFromSnd_NonGodotEntity_ReturnsNull()
    {
        var stubEntity = new StubSndEntity("stub");
        var result = stubEntity.GetNodeFromSnd<Node>("any");
        IntegrationTestRunner.AssertNull(result, "result");
    }

    private sealed class StubNodeHandle(string name) : INodeHandle
    {
        public string Name => name;
        public void Free() { }
        public void SetVisible(bool visible) { }
    }

    private sealed class StubSndEntity(string name) : ISndEntity
    {
        public void SetData<T>(string name, T value) { }
        public T GetData<T>(string name) => default!;
        public (bool found, T? value) TryGetData<T>(string name) => (false, default);
        public INodeHandle GetNode(string name) => new StubNodeHandle(name);
        public System.Collections.Generic.IReadOnlyCollection<string> GetNodeNames() => [];
        public void AddStrategy(string index) { }
        public void RemoveStrategy(string index) { }
        public void AddActiveStrategy(string index) { }
        public void RemoveActiveStrategy(string index) { }
        public object? InvokeStrategy(string strategyIndex, object? input = null) => null;
        public void MountObserverStrategy(string targetName, string observerIndex) { }
        public void UnmountObserverStrategy(string targetName, string observerIndex) { }
        public void MountObserverStrategy(ISndEntity target, string observerIndex) { }
        public void UnmountObserverStrategy(ISndEntity target, string observerIndex) { }
        public string Name => name;
        public bool IsPendingKill => false;
        public Origo.Core.Abstractions.Lifecycle.ISessionRun OwningSession => null!;
    }
}
