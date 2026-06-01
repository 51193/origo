using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     功能完整的纯内存 <see cref="ISndSceneHost" /> 实现。
///     与 <see cref="MemorySndSceneHost" /> 不同，此实现通过 <see cref="SndWorld.CreateEntity" /> 创建
///     真正的 <see cref="SndEntity" />，具备完整的策略生命周期（所有 Hook）、数据订阅与 Process 能力。
///     不依赖任何引擎适配层，节点创建由 <see cref="NullNodeFactory" /> 提供空操作实现。
///     <para>
///         此宿主用于后台关卡等需要完整 Core 逻辑但不需要引擎节点的场景。
///         通过 <see cref="SndContext.CreateBackgroundSession" /> 创建的后台 <see cref="SessionRun" />
///         即注入此宿主实例。
///     </para>
/// </summary>
internal sealed class FullMemorySndSceneHost : ISndSceneHost, ISndContextAttachableSceneHost
{
    private readonly List<MemoryEntityEntry> _entries = new();
    private readonly ILogger _logger;
    private readonly NullNodeFactory _nodeFactory = new();
    private ISndContext? _context;
    private SndWorld? _world;

    /// <summary>
    ///     创建功能完整的内存场景宿主。
    ///     <see cref="SndWorld" /> 和 <see cref="ISndContext" /> 通过
    ///     <see cref="BindWorld" /> 和 <see cref="BindContext" /> 延迟绑定，
    ///     以配合 <see cref="OrigoRuntime" /> 的两阶段构造流程。
    /// </summary>
    /// <param name="logger">日志服务。</param>
    public FullMemorySndSceneHost(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    ///     绑定 <see cref="ISndContext" />，用于策略生命周期回调。
    ///     必须在首次 Spawn/Load 之前调用。
    /// </summary>
    public void BindContext(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public ISndEntity Spawn(SndMetaData metaData)
    {
        ArgumentNullException.ThrowIfNull(metaData);
        EnsureReady();
        var entity = _world!.CreateEntity(_nodeFactory, _context!, _logger);
        entity.Name = metaData.Name;
        _entries.Add(new MemoryEntityEntry(entity));
        entity.Spawn(metaData);
        return entity;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ISndEntity> GetEntities() => _entries.Select(e => (ISndEntity)e.Entity).ToArray();

    /// <inheritdoc />
    public ISndEntity? FindByName(string name)
    {
        return _entries.FirstOrDefault(e =>
            string.Equals(e.Entity.Name, name, StringComparison.Ordinal))?.Entity;
    }

    /// <inheritdoc />
    public IReadOnlyList<SndMetaData> SerializeMetaList()
    {
        var list = new List<SndMetaData>(_entries.Count);
        foreach (var entry in _entries)
            list.Add(entry.Entity.SerializeMetaData());
        return list;
    }

    /// <inheritdoc />
    public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
    {
        ArgumentNullException.ThrowIfNull(metaList);
        EnsureReady();

        // Load 语义：先清除旧实体，再按元数据创建新实体并触发 AfterLoad。
        QuitAll();
        foreach (var meta in metaList)
        {
            var entity = _world!.CreateEntity(_nodeFactory, _context!, _logger);
            entity.Name = meta.Name;
            _entries.Add(new MemoryEntityEntry(entity));
            entity.Load(meta);
        }
    }

    /// <inheritdoc />
    public void ClearAll() => QuitAll();

    /// <inheritdoc />
    public void DeadByName(string name)
    {
        var index = _entries.FindIndex(e =>
            string.Equals(e.Entity.Name, name, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"No entity with name '{name}'.");

        var entry = _entries[index];
        _entries.RemoveAt(index);
        entry.Entity.Dead();
    }

    /// <inheritdoc />
    public void RequestKillEntity(string name)
    {
        var index = _entries.FindIndex(e =>
            string.Equals(e.Entity.Name, name, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"No entity with name '{name}'.");

        var entry = _entries[index];
        if (entry.Entity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");

        entry.Entity.IsPendingKill = true;
    }

    private void QuitAll()
    {
        // 反向退出以匹配 LIFO 语义。
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            _entries.RemoveAt(i);
            entry.Entity.Quit();
        }
    }

    /// <summary>
    ///     对所有存活实体执行 Process 帧更新。
    /// </summary>
    /// <param name="delta">帧间隔时间（秒）。</param>
    public void ProcessAll(double delta)
    {
        // 基于快照迭代，允许 Process 中增删实体。
        var snapshot = _entries.ToArray();
        foreach (var entry in snapshot)
            entry.Entity.Process(delta);
    }

    /// <summary>
    ///     绑定 <see cref="SndWorld" />，用于通过 <see cref="SndWorld.CreateEntity" /> 创建实体。
    ///     必须在首次 Spawn/Load 之前调用。
    /// </summary>
    internal void BindWorld(SndWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    private void EnsureReady()
    {
        if (_world is null)
            throw new InvalidOperationException(
                "SndWorld is not bound. Call BindWorld before spawning or loading entities.");
        if (_context is null)
            throw new InvalidOperationException(
                "ISndContext is not bound. Call BindContext before spawning or loading entities.");
    }

    private sealed class MemoryEntityEntry
    {
        public MemoryEntityEntry(SndEntity entity)
        {
            Entity = entity;
        }

        public SndEntity Entity { get; }
    }
}
