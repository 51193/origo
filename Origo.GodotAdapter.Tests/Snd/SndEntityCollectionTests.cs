using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Snd;
using Xunit;

namespace Origo.GodotAdapter.Tests;

internal sealed class FakeSndEntity : ISndEntityFacade
{
    private readonly List<SndMetaData> _recovered = [];

    public string Name { get; private set; } = "";
    public string StableName { get; private set; } = "";
    public bool IsPendingKill { get; private set; }
    public ISessionRun OwningSession { get; private set; } = null!;
    public int ProcessCount { get; private set; }
    public int DetachCount { get; private set; }
    public bool FailRecover { get; set; }

    public Action? OnProcess { get; set; }

    public IReadOnlyList<SndMetaData> Recovered => _recovered;

    public void RecoverForLifecycle(SndMetaData meta)
    {
        if (FailRecover)
            throw new InvalidOperationException("recover failed");
        _recovered.Add(meta);
        Name = meta.Name;
        StableName = meta.Name;
    }

    public void BindSession(ISessionRun session) => OwningSession = session;

    public void ProcessSnd(double delta)
    {
        ProcessCount++;
        OnProcess?.Invoke();
    }

    public void DetachFromManager() => DetachCount++;

    public void MarkPendingKill() => IsPendingKill = true;

    public SndMetaData BuildSndMetaData() => new() { Name = StableName };

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

public class SndEntityCollectionTests
{
    private static SndEntityCollection<FakeSndEntity> CreateCollection(out List<string> detached)
    {
        var detachLog = new List<string>();
        detached = detachLog;
        var collection = new SndEntityCollection<FakeSndEntity>(
            () => new FakeSndEntity(),
            entity => detachLog.Add(entity.StableName));
        return collection;
    }

    private static SndMetaData Meta(string name) => new() { Name = name };

    [Fact]
    public void CreateEntity_AddsAndRecovers()
    {
        var collection = CreateCollection(out _);
        var entity = collection.CreateEntity(Meta("e1"));

        var fake = Assert.IsType<FakeSndEntity>(entity);
        Assert.Equal("e1", fake.StableName);
        Assert.Single(collection);
        Assert.Single(fake.Recovered);
    }

    [Fact]
    public void CreateEntity_NullMeta_Throws()
    {
        var collection = CreateCollection(out _);
        Assert.Throws<ArgumentNullException>(() => collection.CreateEntity(null!));
    }

    [Fact]
    public void CreateEntity_RecoverFailure_RollsBackAndPropagates()
    {
        var failing = new FakeSndEntity { FailRecover = true };
        var collection2 = new SndEntityCollection<FakeSndEntity>(() => failing);

        var ex = Assert.Throws<InvalidOperationException>(() => collection2.CreateEntity(Meta("boom")));
        Assert.Contains("recover failed", ex.Message);
        Assert.Empty(collection2);
        Assert.Equal(1, failing.DetachCount);
    }

    [Fact]
    public void CreateEntity_OwningSession_BindsEntity()
    {
        var collection = CreateCollection(out _);
        var session = new FakeSession();
        collection.OwningSession = session;

        var entity = collection.CreateEntity(Meta("e1"));

        Assert.Same(session, Assert.IsType<FakeSndEntity>(entity).OwningSession);
    }

    [Fact]
    public void RecoverFromMetaList_RecoversAll()
    {
        var collection = CreateCollection(out _);

        collection.RecoverFromMetaList([Meta("a"), Meta("b"), Meta("c")]);

        Assert.Equal(3, collection.Count);
        Assert.NotNull(collection.FindByName("a"));
        Assert.NotNull(collection.FindByName("c"));
    }

    [Fact]
    public void RecoverFromMetaList_Failure_RollsBackStaged()
    {
        var collection = CreateCollection(out _);
        var failing = new FakeSndEntity { FailRecover = true };

        var collection2 = new SndEntityCollection<FakeSndEntity>(() => failing);

        Assert.Throws<InvalidOperationException>(() =>
            collection2.RecoverFromMetaList([Meta("a"), Meta("b"), Meta("boom")]));
        Assert.Empty(collection2);
        Assert.Equal(1, failing.DetachCount);
    }

    [Fact]
    public void RecoverFromMetaList_Failure_ReportsFailingMeta()
    {
        var collection = CreateCollection(out _);
        var failing = new FakeSndEntity { FailRecover = true };
        var factories = new Queue<FakeSndEntity>([new FakeSndEntity(), failing]);
        var collection2 = new SndEntityCollection<FakeSndEntity>(() => factories.Dequeue());
        SndMetaData? reported = null;
        Exception? reportedException = null;

        Assert.Throws<InvalidOperationException>(() =>
            collection2.RecoverFromMetaList([Meta("a"), Meta("boom")],
                (meta, ex) =>
                {
                    reported = meta;
                    reportedException = ex;
                }));

        Assert.Equal("boom", reported?.Name);
        Assert.NotNull(reportedException);
        Assert.Contains("recover failed", reportedException!.Message);
    }

    [Fact]
    public void RecoverFromMetaList_Null_Throws()
    {
        var collection = CreateCollection(out _);
        Assert.Throws<ArgumentNullException>(() => collection.RecoverFromMetaList(null!));
    }

    [Fact]
    public void FindByName_ReturnsEntity()
    {
        var collection = CreateCollection(out _);
        collection.CreateEntity(Meta("e1"));
        collection.CreateEntity(Meta("e2"));

        Assert.Equal("e1", collection.FindByName("e1")!.Name);
        Assert.Null(collection.FindByName("missing"));
    }

    [Fact]
    public void RemoveEntity_DetachesAndRemoves()
    {
        var collection = CreateCollection(out var detached);
        collection.CreateEntity(Meta("e1"));

        collection.RemoveEntity("e1");

        Assert.Empty(collection);
        Assert.Equal(["e1"], detached);
    }

    [Fact]
    public void RemoveEntity_Unknown_Throws()
    {
        var collection = CreateCollection(out _);
        Assert.Throws<InvalidOperationException>(() => collection.RemoveEntity("nope"));
    }

    [Fact]
    public void RequestKillEntity_MarksPending()
    {
        var collection = CreateCollection(out _);
        var entity = (FakeSndEntity)collection.CreateEntity(Meta("e1"));

        collection.RequestKillEntity("e1");

        Assert.True(entity.IsPendingKill);
    }

    [Fact]
    public void RequestKillEntity_AlreadyPending_Throws()
    {
        var collection = CreateCollection(out _);
        collection.CreateEntity(Meta("e1"));
        collection.RequestKillEntity("e1");

        Assert.Throws<InvalidOperationException>(() => collection.RequestKillEntity("e1"));
    }

    [Fact]
    public void RequestKillEntity_Unknown_Throws()
    {
        var collection = CreateCollection(out _);
        Assert.Throws<InvalidOperationException>(() => collection.RequestKillEntity("nope"));
    }

    [Fact]
    public void ProcessAll_ProcessesEveryEntity()
    {
        var collection = CreateCollection(out _);
        var a = (FakeSndEntity)collection.CreateEntity(Meta("a"));
        var b = (FakeSndEntity)collection.CreateEntity(Meta("b"));

        collection.ProcessAll(0.5);

        Assert.Equal(1, a.ProcessCount);
        Assert.Equal(1, b.ProcessCount);
    }

    [Fact]
    public void ProcessAll_ContainerModifiedDuringProcess_Throws()
    {
        // Matches FullMemorySndSceneHost: mutating the host while entities
        // process must fail loudly instead of silently skipping/adding frames.
        var collection = CreateCollection(out _);
        var a = (FakeSndEntity)collection.CreateEntity(Meta("a"));
        a.OnProcess = () => collection.CreateEntity(Meta("spawned_during_process"));

        var ex = Assert.Throws<InvalidOperationException>(() => collection.ProcessAll(0.5));
        Assert.Contains("modified during ProcessAll", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveAllEntities_ClearsCollection()
    {
        var collection = CreateCollection(out var detached);
        collection.CreateEntity(Meta("a"));
        collection.CreateEntity(Meta("b"));

        collection.RemoveAllEntities();

        Assert.Empty(collection);
        Assert.Equal(["b", "a"], detached);
    }

    [Fact]
    public void BuildMetaList_ReturnsAllMetadata()
    {
        var collection = CreateCollection(out _);
        collection.CreateEntity(Meta("a"));
        collection.CreateEntity(Meta("b"));

        var metas = collection.BuildMetaList();

        Assert.Equal(2, metas.Count);
        Assert.Equal("a", metas[0].Name);
        Assert.Equal("b", metas[1].Name);
    }

    [Fact]
    public void GetEntities_ReturnsAllAndIsEnumerable()
    {
        var collection = CreateCollection(out _);
        collection.CreateEntity(Meta("a"));
        collection.CreateEntity(Meta("b"));

        var entities = collection.GetEntities();
        Assert.Equal(2, entities.Count);

        var names = new List<string>();
        foreach (var entity in collection)
            names.Add(entity.Name);
        Assert.Equal(["a", "b"], names);
    }

    [Fact]
    public void GetEntities_ReturnsSnapshot_NotTheMutableBackingList()
    {
        var collection = CreateCollection(out _);
        collection.CreateEntity(Meta("a"));

        var snapshot = collection.GetEntities();

        // The snapshot is a copy: mutating the collection afterwards does not
        // change an already-obtained view, and the view cannot be downcast to
        // the mutable backing list (which would bypass collection management).
        Assert.NotSame(collection, snapshot);
        Assert.False(snapshot is List<FakeSndEntity>);

        var before = snapshot.ToList();
        collection.CreateEntity(Meta("b"));
        Assert.Single(before);
        Assert.Equal(2, collection.Count);
    }
}

internal sealed class FakeSession : ISessionRun
{
    public IBlackboard SessionBlackboard => throw new NotSupportedException();
    public string LevelId => "fake_level";
    public bool IsFrontSession => true;
    public ISessionManager SessionManager => throw new NotSupportedException();
    public IStateMachineContainer GetSessionStateMachines() => throw new NotSupportedException();
    public ISndEntity? FindByName(string name) => null;
    public IReadOnlyCollection<ISndEntity> GetEntities() => [];
    public ISndEntity Spawn(SndMetaData meta) => throw new NotSupportedException();
    public void SpawnMany(params SndMetaData[] metaList) => throw new NotSupportedException();
    public void RequestKillEntity(string entityName) => throw new NotSupportedException();
    public void Dispose() => throw new NotSupportedException();
}
