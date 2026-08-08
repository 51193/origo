using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

[Collection("TypedData")]
public class SndDataManagerFailureTests
{
    public SndDataManagerFailureTests()
    {
        TypedData.ResetForTesting();
    }

    [Fact]
    public void SetData_ConverterThrows_LeavesNoDictionaryEntry()
    {
        // A custom kind whose object conversion always throws: SetData must
        // fail without leaving a default (null) entry behind that would
        // otherwise leak into serialized saves.
        TypedDataLayeredRegistry.RegisterKindResolver(t => t == typeof(Version) ? (byte)220 : (byte)0);
        TypedDataLayeredRegistry.RegisterFromObjectFallback((kind, value) =>
            kind == 220 ? throw new InvalidOperationException("converter failed") : null);

        var manager = new SndDataManager(new DummyEntity("E"), new TestLogger());

        Assert.Throws<InvalidOperationException>(() => manager.SetData("hp", new Version(1, 0)));

        var meta = manager.SerializeMeta();
        Assert.Empty(meta.Pairs);

        var (found, _) = manager.TryGetData<int>("hp");
        Assert.False(found, "Failed SetData must not leave a key behind.");
    }

    [Fact]
    public void SetData_WhenObserverThrows_ValueStillCommittedAndRemainingObserversSkipped()
    {
        // Contract: data is committed before observers are notified; a
        // throwing observer aborts the remaining notifications and the
        // exception propagates (fail-fast). The committed value must be
        // readable afterwards — a failed notification does not roll back data.
        var manager = new SndDataManager(new DummyEntity("E"), new TestLogger());
        var remainingCalled = false;
        manager.Subscribe("hp", (_, _, _) => throw new InvalidOperationException("observer failure"), null);
        manager.Subscribe("hp", (_, _, _) => remainingCalled = true, null);

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SetData("hp", 50));
        Assert.Equal("observer failure", ex.Message);
        Assert.False(remainingCalled, "A throwing observer must abort the remaining notifications.");

        var (found, value) = manager.TryGetData<int>("hp");
        Assert.True(found, "The value must remain committed after a failed notification.");
        Assert.Equal(50, value);
    }

    [Fact]
    public void TryGetData_BlankName_Throws()
    {
        var manager = new SndDataManager(new DummyEntity("E"), new TestLogger());

        Assert.Throws<ArgumentException>(() => manager.TryGetData<int>(""));
        Assert.Throws<ArgumentException>(() => manager.TryGetData<int>("", out _));
        Assert.Throws<ArgumentException>(() => manager.GetRequiredData<int>(""));
    }

    private sealed class DummyEntity(string name) : ISndEntity
    {
        public string Name { get; } = name;
        public bool IsPendingKill { get; set; }
        public ISessionRun OwningSession => throw new NotSupportedException();
        public void SetData<T>(string name, T value) => throw new NotSupportedException();
        public T GetData<T>(string name) where T : notnull => throw new NotSupportedException();
        public (bool found, T? value) TryGetData<T>(string name) => throw new NotSupportedException();
        public bool TryGetData<T>(string name, out T? value) => throw new NotSupportedException();
        public void MountObserverStrategy(string targetName, string observerIndex) => throw new NotSupportedException();
        public void UnmountObserverStrategy(string targetName, string observerIndex) => throw new NotSupportedException();
        public void MountObserverStrategy(ISndEntity target, string observerIndex) => throw new NotSupportedException();
        public void UnmountObserverStrategy(ISndEntity target, string observerIndex) => throw new NotSupportedException();
        public INodeHandle GetNode(string name) => throw new NotSupportedException();
        public IReadOnlyCollection<string> GetNodeNames() => [];
        public void AddStrategy(string index) => throw new NotSupportedException();
        public void RemoveStrategy(string index) => throw new NotSupportedException();
        public void AddActiveStrategy(string index) => throw new NotSupportedException();
        public void RemoveActiveStrategy(string index) => throw new NotSupportedException();
        public object? InvokeStrategy(string strategyIndex, object? input = null) => throw new NotSupportedException();
    }
}
