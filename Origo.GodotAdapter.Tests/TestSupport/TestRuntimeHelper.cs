using System;
using System.Collections.Generic;
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.TestSupport;

namespace Origo.GodotAdapter.Tests;

internal sealed class TestSndSceneHost : ISndSceneHost
{
    private readonly Dictionary<string, ISndEntity> _entities = [];

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities.Values;

    public ISndEntity? FindByName(string name) =>
        _entities.TryGetValue(name, out var entity) ? entity : null;

    public void ProcessAll(double delta)
    {
    }

    public ISndEntity CreateEntity(SndMetaData metaData) =>
        throw new NotSupportedException("CreateEntity not supported in test scene host.");

    public void RemoveAllEntities() => _entities.Clear();

    public IReadOnlyList<SndMetaData> BuildMetaList() => [];

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
    }

    public void RemoveEntity(string name)
    {
        if (_entities.TryGetValue(name, out _)) _entities.Remove(name);
    }

    public void RequestKillEntity(string name)
    {
        if (!_entities.TryGetValue(name, out var entity))
            throw new InvalidOperationException($"No entity with name '{name}'.");
        if (entity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        ((InMemorySndEntity)entity).IsPendingKill = true;
    }

    public void AddEntity(ISndEntity entity) => _entities[entity.Name] = entity;
}

internal sealed class InMemorySndEntity : ISndEntity
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    public InMemorySndEntity(string name)
    {
        _data["name"] = name;
    }

    public string Name => (string)_data["name"]!;
    public bool IsPendingKill { get; set; }
    public ISessionRun OwningSession { get; set; } = null!;

    public void SetData<T>(string name, T value) => _data[name] = value;
    public T GetData<T>(string name) where T : notnull => _data.TryGetValue(name, out var v) && v is T c ? c : default!;
    public (bool found, T? value) TryGetData<T>(string name) =>
        _data.TryGetValue(name, out var v) && v is T c ? (true, c) : (false, default);
    public bool TryGetData<T>(string name, out T? value)
    {
        var (found, stored) = TryGetData<T>(name);
        value = stored;
        return found;
    }


    public void MountObserverStrategy(string targetName, string observerIndex) { }
    public void UnmountObserverStrategy(string targetName, string observerIndex) { }
    public void MountObserverStrategy(ISndEntity target, string observerIndex) { }
    public void UnmountObserverStrategy(ISndEntity target, string observerIndex) { }

    public INodeHandle GetNode(string name) =>
        throw new InvalidOperationException($"Node '{name}' not found.");
    public IReadOnlyCollection<string> GetNodeNames() => [];
    public void AddStrategy(string index) { }
    public void RemoveStrategy(string index) { }
    public void AddActiveStrategy(string index) { }
    public void RemoveActiveStrategy(string index) { }
    public object? InvokeStrategy(string strategyIndex, object? input = null) => null;
}

internal static class TestRuntimeHelper
{
    public static (OrigoRuntime runtime, TestSndSceneHost sceneHost) CreateRuntime()
    {
        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(tm);
        var io = DataSourceFactory.CreateDefaultIoGateway(new NullFileSystem());
        var bb = new Blackboard();

        var runtime = new OrigoRuntime(
            new OrigoMeta("Origo", "test", string.Empty),
            logger,
            sceneHost,
            tm,
            registry,
            io,
            bb);

        return (runtime, sceneHost);
    }

    public static void BootstrapForegroundSession(OrigoRuntime runtime)
    {
        var fs = new MemoryFileSystem();
        fs.WriteAllText("entry.json", "[]", true);
        var io = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);

        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
    }
}
