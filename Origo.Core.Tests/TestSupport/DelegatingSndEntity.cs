using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

/// <summary>
///     Test double that mimics an adapter-layer bridge entity (e.g. the Godot
///     adapter's <c>GodotSndEntity</c>): wraps an inner Core
///     <see cref="SndEntity" /> and delegates every <see cref="ISndEntity" />,
///     <see cref="ISndEntityRawSubscription" />, and <see cref="IEntityLifecycle" />
///     member to it. Strategy operations therefore route through a real
///     <c>SndStrategyManager</c> while the outer type is not itself an
///     <see cref="SndEntity" /> — the exact contract the plan engine must
///     handle for bridge entities.
/// </summary>
internal sealed class DelegatingSndEntity : ISndEntity, ISndEntityRawSubscription, IEntityLifecycle
{
    private readonly SndEntity _inner;

    public DelegatingSndEntity(SndEntity inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Exposes the wrapped Core entity for framework-side assertions.</summary>
    public SndEntity Inner => _inner;

    public string Name => _inner.Name;

    public bool IsPendingKill => _inner.IsPendingKill;

    public ISessionRun OwningSession => _inner.OwningSession;

    public void SetData<T>(string name, T value) => _inner.SetData(name, value);

    public T GetData<T>(string name) where T : notnull => _inner.GetData<T>(name);

    public (bool found, T? value) TryGetData<T>(string name) => _inner.TryGetData<T>(name);

    public bool TryGetData<T>(string name, out T? value) => _inner.TryGetData<T>(name, out value);

    public INodeHandle GetNode(string name) => _inner.GetNode(name);

    public IReadOnlyCollection<string> GetNodeNames() => _inner.GetNodeNames();

    public void AddStrategy(string index) => _inner.AddStrategy(index);

    public void RemoveStrategy(string index) => _inner.RemoveStrategy(index);

    public void AddActiveStrategy(string index) => _inner.AddActiveStrategy(index);

    public void RemoveActiveStrategy(string index) => _inner.RemoveActiveStrategy(index);

    public object? InvokeStrategy(string strategyIndex, object? input = null) => _inner.InvokeStrategy(strategyIndex, input);

    public void MountObserverStrategy(string targetName, string observerIndex) => _inner.MountObserverStrategy(targetName, observerIndex);

    public void UnmountObserverStrategy(string targetName, string observerIndex) => _inner.UnmountObserverStrategy(targetName, observerIndex);

    public void MountObserverStrategy(ISndEntity target, string observerIndex) => _inner.MountObserverStrategy(target, observerIndex);

    public void UnmountObserverStrategy(ISndEntity target, string observerIndex) => _inner.UnmountObserverStrategy(target, observerIndex);

    void ISndEntityRawSubscription.SubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter)
        => ((ISndEntityRawSubscription)_inner).SubscribeDataRaw(name, callback, filter);

    void ISndEntityRawSubscription.UnsubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback)
        => ((ISndEntityRawSubscription)_inner).UnsubscribeDataRaw(name, callback);

    void IEntityLifecycle.RecoverForLifecycle(SndMetaData metaData) => ((IEntityLifecycle)_inner).RecoverForLifecycle(metaData);

    void IEntityLifecycle.FireAfterSpawnHooks() => ((IEntityLifecycle)_inner).FireAfterSpawnHooks();

    void IEntityLifecycle.FireAfterLoadHooks() => ((IEntityLifecycle)_inner).FireAfterLoadHooks();

    void IEntityLifecycle.FireBeforeSaveHooks() => ((IEntityLifecycle)_inner).FireBeforeSaveHooks();

    void IEntityLifecycle.FireBeforeQuitHooks() => ((IEntityLifecycle)_inner).FireBeforeQuitHooks();

    void IEntityLifecycle.FireBeforeDeadHooks() => ((IEntityLifecycle)_inner).FireBeforeDeadHooks();

    void IEntityLifecycle.ReleaseStrategiesOnly() => ((IEntityLifecycle)_inner).ReleaseStrategiesOnly();

    void IEntityLifecycle.TeardownOnly() => ((IEntityLifecycle)_inner).TeardownOnly();

    void IEntityLifecycle.TeardownObserverBindings() => ((IEntityLifecycle)_inner).TeardownObserverBindings();

    SndMetaData IEntityLifecycle.BuildMetaData() => ((IEntityLifecycle)_inner).BuildMetaData();
}
