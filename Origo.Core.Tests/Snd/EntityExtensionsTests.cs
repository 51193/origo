using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class EntityExtensionsTests
{
    [Fact]
    public void IsSameEntityAs_SameReference_ReturnsTrue()
    {
        var entity = new StubEntity("hero");

        Assert.True(entity.IsSameEntityAs(entity));
    }

    [Fact]
    public void IsSameEntityAs_DifferentWrappersSameEntity_ReturnsTrue()
    {
        var session = new StubSession();
        var inner = new StubEntity("hero") { OwningSession = session };
        var wrapper = new StubEntity("hero") { OwningSession = session };

        Assert.True(inner.IsSameEntityAs(wrapper));
    }

    [Fact]
    public void IsSameEntityAs_SameNameDifferentSession_ReturnsFalse()
    {
        var first = new StubEntity("hero") { OwningSession = new StubSession() };
        var second = new StubEntity("hero") { OwningSession = new StubSession() };

        Assert.False(first.IsSameEntityAs(second));
    }

    [Fact]
    public void IsSameEntityAs_DifferentNamesSameSession_ReturnsFalse()
    {
        var session = new StubSession();
        var first = new StubEntity("hero") { OwningSession = session };
        var second = new StubEntity("villain") { OwningSession = session };

        Assert.False(first.IsSameEntityAs(second));
    }

    [Fact]
    public void IsSameEntityAs_NullArgument_Throws()
    {
        var entity = new StubEntity("hero");

        Assert.Throws<System.ArgumentNullException>(() => entity.IsSameEntityAs(null!));
    }


    [Fact]
    public void IsSameEntityAs_SameNameBothUnbound_ReturnsTrue()
    {
        // Unbound stubs (no owning session) degenerate to name equality;
        // unbound containers enforce unique names.
        var first = new StubEntity("hero") { OwningSession = null! };
        var second = new StubEntity("hero") { OwningSession = null! };

        Assert.True(first.IsSameEntityAs(second));
    }

    [Fact]
    public void IsSameEntityAs_OneBoundOneUnbound_ReturnsFalse()
    {
        var bound = new StubEntity("hero") { OwningSession = new StubSession() };
        var unbound = new StubEntity("hero") { OwningSession = null! };

        Assert.False(bound.IsSameEntityAs(unbound));
    }
    private sealed class StubEntity(string name) : Origo.Core.Abstractions.Entity.ISndEntity
    {
        public ISessionRun OwningSession { get; init; } = null!;
        public string Name => name;
        public bool IsPendingKill => false;

        public object? InvokeStrategy(string strategyIndex, object? input = null) => null;

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

    private sealed class StubSession : ISessionRun
    {
        public IBlackboard SessionBlackboard => throw new System.NotImplementedException();
        public string LevelId => "test";
        public bool IsFrontSession => true;
        public ISessionManager SessionManager => throw new System.NotImplementedException();

        public Origo.Core.Abstractions.Entity.ISndEntity? FindByName(string name) => throw new System.NotImplementedException();
        public IReadOnlyCollection<Origo.Core.Abstractions.Entity.ISndEntity> GetEntities() => throw new System.NotImplementedException();
        public Origo.Core.Abstractions.Entity.ISndEntity Spawn(Origo.Core.Snd.Metadata.SndMetaData meta) => throw new System.NotImplementedException();
        public void SpawnMany(params Origo.Core.Snd.Metadata.SndMetaData[] metaList) => throw new System.NotImplementedException();
        public void RequestKillEntity(string entityName) => throw new System.NotImplementedException();
        public Origo.Core.Abstractions.StateMachine.IStateMachineContainer GetSessionStateMachines() => throw new System.NotImplementedException();
        public void Dispose() { }
    }
}
