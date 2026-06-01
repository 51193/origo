using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     管理场景内 GodotSndEntity 集合的适配层管理器。
///     同时实现 ISndSceneHost，供 Core 层的 SndRuntime 以抽象方式进行实体操作。
///     使用内部 List 维护实体集合，避免每帧通过 Group 查询造成 GC 压力。
/// </summary>
[GlobalClass]
public partial class GodotSndManager : Node, ISndSceneHost, ISndContextAttachableSceneHost
{
    private readonly List<GodotSndEntity> _entities = new();
    private readonly List<GodotSndEntity> _processBuffer = new();
    private EntityView? _entityView;
    private bool _inProcess;

    private bool _runtimeDepsBound;

    public SndWorld SharedWorld { get; private set; } = null!;
    public ILogger SharedLogger { get; private set; } = null!;
    public ISndContext? Context { get; private set; }
    public int ProcessTickCount { get; private set; }
    public double ProcessDeltaSum { get; private set; }

    /// <summary>
    ///     绑定存档/生命周期门面。
    ///     支持在生命周期切换时重新绑定会话上下文。
    /// </summary>
    public void BindContext(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_runtimeDepsBound) throw new InvalidOperationException("Call BindRuntimeDependencies before BindContext.");

        Context = context;
    }

    public IReadOnlyList<SndMetaData> SerializeMetaList()
    {
        var list = new List<SndMetaData>(_entities.Count);
        for (var i = 0; i < _entities.Count; i++)
            list.Add(_entities[i].SerializeMetaData());
        return list;
    }

    public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        var staged = new List<GodotSndEntity>();
        foreach (var meta in metaList)
        {
            GodotSndEntity? snd = null;
            try
            {
                snd = CreateSndEntity();
                AddChild(snd);
                _entities.Add(snd);
                staged.Add(snd);
                snd.Load(meta);
            }
            catch
            {
                RollbackPartialLoad(staged);
                if (snd is not null && IsInstanceValid(snd))
                {
                    _entities.Remove(snd);
                    if (snd.GetParent() == this)
                        RemoveChild(snd);
                    snd.Free();
                }

                throw;
            }
        }
    }

    public void ClearAll() => QuitAll();

    public ISndEntity Spawn(SndMetaData metaData) => SpawnFromMeta(metaData);

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entityView ??= new EntityView(_entities);

    public ISndEntity? FindByName(string name)
    {
        var entity = _entities.FirstOrDefault(s => s.StableName == name);
        return entity;
    }

    public void ProcessAll(double delta)
    {
        // Delegate to the Godot _Process-equivalent logic for non-Godot callers (e.g. background session ticking).
        var snapshot = _entities.ToArray();
        foreach (var entity in snapshot)
            entity.ProcessSnd(delta);
    }

    public GodotSndEntity SpawnFromMeta(SndMetaData metaData)
    {
        var staged = new List<GodotSndEntity>();
        try
        {
            var snd = CreateSndEntity();
            AddChild(snd);
            _entities.Add(snd);
            staged.Add(snd);
            snd.Spawn(metaData);
            return snd;
        }
        catch
        {
            RollbackPartialLoad(staged);
            throw;
        }
    }

    /// <summary>
    ///     绑定与 Core 运行时共享的 SndWorld 与日志（由 <see cref="Bootstrap.OrigoAutoHost" /> 调用一次）。
    /// </summary>
    public void BindRuntimeDependencies(SndWorld world, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(logger);

        if (_runtimeDepsBound) throw new InvalidOperationException("Runtime dependencies are already bound.");

        SharedWorld = world;
        SharedLogger = logger;
        _runtimeDepsBound = true;
    }

    public void QuitAll()
    {
        // 严格栈语义：如果栈 push 顺序与实体 load/spawn 顺序一致，则退出顺序应当反向以匹配 LIFO。
        for (var i = _entities.Count - 1; i >= 0; i--)
        {
            var snd = _entities[i];
            _entities.RemoveAt(i);
            snd.QuitFromManager();
        }
    }

    public void DeadByName(string name)
    {
        var snd = _entities.FirstOrDefault(s => s.StableName == name);
        if (snd is null)
            throw new InvalidOperationException($"No entity with StableName '{name}'.");

        _entities.Remove(snd);
        snd.DeadFromManager();
    }

    public void RequestKillEntity(string name)
    {
        var snd = _entities.FirstOrDefault(s => s.StableName == name);
        if (snd is null)
            throw new InvalidOperationException($"No entity with StableName '{name}'.");

        if (snd.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");

        snd.MarkPendingKill();
    }

    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta)
    {
        if (_inProcess)
            throw new InvalidOperationException("GodotSndManager._Process re-entrancy is not allowed.");

        _inProcess = true;
        try
        {
            ProcessTickCount++;
            ProcessDeltaSum += delta;
            _processBuffer.Clear();
            _processBuffer.AddRange(_entities);
            for (var i = 0; i < _processBuffer.Count; i++)
                _processBuffer[i].ProcessSnd(delta);
            Context?.FlushDeferredActionsForCurrentFrame();
        }
        finally
        {
            _inProcess = false;
        }
    }

    private void RollbackPartialLoad(List<GodotSndEntity> staged)
    {
        for (var i = staged.Count - 1; i >= 0; i--)
        {
            var s = staged[i];
            _entities.Remove(s);
            if (IsInstanceValid(s) && s.GetParent() == this)
                RemoveChild(s);
            if (IsInstanceValid(s))
                s.Free();
        }
    }

    private GodotSndEntity CreateSndEntity()
    {
        EnsureReadyForSpawn();
        return new GodotSndEntity(SharedWorld, Context!, SharedLogger,
            entity => new GodotPackedSceneNodeFactory(entity));
    }

    private void EnsureReadyForSpawn()
    {
        if (!_runtimeDepsBound || Context is null)
            throw new InvalidOperationException(
                "GodotSndManager is not ready: call BindRuntimeDependencies and BindContext before spawning entities.");
    }

    private sealed class EntityView(List<GodotSndEntity> inner) : IReadOnlyList<ISndEntity>
    {
        public int Count => inner.Count;

        public ISndEntity this[int index] => inner[index];

        public IEnumerator<ISndEntity> GetEnumerator()
        {
            for (var i = 0; i < inner.Count; i++)
                yield return inner[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
