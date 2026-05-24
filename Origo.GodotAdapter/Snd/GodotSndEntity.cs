using System;
using System.Collections.Generic;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     将 Core 的 SndEntity 绑定到 Godot Node 生命周期。
///     所有依赖通过构造函数注入。
/// </summary>
[GlobalClass]
public partial class GodotSndEntity : Node, ISndEntity
{
    private readonly ISndContext _context;
    private readonly ILogger _logger;
    private readonly Func<GodotSndEntity, INodeFactory> _nodeFactoryCreator;
    private readonly SndWorld _world;
    private SndEntity? _entity;
    private bool _releasedFromManager;

    public GodotSndEntity(
        SndWorld world,
        ISndContext context,
        ILogger logger,
        Func<GodotSndEntity, INodeFactory> nodeFactoryCreator)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(nodeFactoryCreator);
        _world = world;
        _context = context;
        _logger = logger;
        _nodeFactoryCreator = nodeFactoryCreator;
    }

    /// <summary>
    ///     稳定的实体名（用于 Core 查找 key）。
    ///     注意：Godot Node 的 Name 可能因同名冲突而被自动重命名，因此 FindByName 不应依赖 Name。
    /// </summary>
    internal string StableName { get; private set; } = string.Empty;

    string ISndEntity.Name => StableName;

    public void SetData<T>(string name, T value)
    {
        EnsureEntity();
        _entity!.SetData(name, value);
    }

    public T GetData<T>(string name)
    {
        EnsureEntity();
        return _entity!.GetData<T>(name);
    }

    public (bool found, T? value) TryGetData<T>(string name)
    {
        EnsureEntity();
        return _entity!.TryGetData<T>(name);
    }

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null)
    {
        EnsureEntity();
        _entity!.Subscribe(name, callback, filter);
    }

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
        EnsureEntity();
        _entity!.Unsubscribe(name, callback);
    }

    public INodeHandle GetNode(string name)
    {
        EnsureEntity();
        return _entity!.GetNode(name);
    }

    public IReadOnlyCollection<string> GetNodeNames()
    {
        EnsureEntity();
        return _entity!.GetNodeNames();
    }

    public void AddStrategy(string index)
    {
        EnsureEntity();
        _entity!.AddStrategy(index);
    }

    public void RemoveStrategy(string index)
    {
        EnsureEntity();
        _entity!.RemoveStrategy(index);
    }

    public TNode? GetNodeFromSnd<TNode>(string name) where TNode : Node
    {
        var handle = GetNode(name);
        return handle?.Native as TNode;
    }

    public void Load(SndMetaData metaData)
    {
        ThrowIfReleasedFromManager();
        ArgumentNullException.ThrowIfNull(metaData);
        // Name 是 FindByName 的 key；必须在触发 AfterLoad 之前可见（Let-it-crash 语义下更应 fail-fast）。
        StableName = metaData.Name;
        Name = metaData.Name;
        EnsureEntity();
        _entity!.Load(metaData);
        StableName = _entity.Name;
        Name = _entity.Name;
    }

    public void Spawn(SndMetaData metaData)
    {
        ThrowIfReleasedFromManager();
        ArgumentNullException.ThrowIfNull(metaData);
        // Name 是 FindByName 的 key；必须在触发 AfterSpawn 之前可见。
        StableName = metaData.Name;
        Name = metaData.Name;
        EnsureEntity();
        _entity!.Spawn(metaData);
        StableName = _entity.Name;
        Name = _entity.Name;
    }

    /// <summary>
    ///     仅由 <see cref="GodotSndManager" /> 调用：从管理列表移除后再释放节点，避免集合与节点生命周期不同步。
    /// </summary>
    internal void QuitFromManager()
    {
        if (_releasedFromManager) return;
        _releasedFromManager = true;
        if (_entity is not null)
        {
            _entity.Quit();
            _entity = null;
        }

        // Core 已有显式生命周期编排，直接使用 Free（即时释放）；详见 README 中 GodotSndEntity 生命周期说明。
        Free();
    }

    /// <summary>
    ///     仅由 <see cref="GodotSndManager" /> 调用。
    /// </summary>
    internal void DeadFromManager()
    {
        if (_releasedFromManager) return;
        _releasedFromManager = true;
        if (_entity is not null)
        {
            _entity.Dead();
            _entity = null;
        }

        Free();
    }

    public SndMetaData SerializeMetaData()
    {
        EnsureEntity();
        return _entity!.SerializeMetaData();
    }

    public void ProcessSnd(double delta) => _entity?.Process(delta);

    private void EnsureEntity()
    {
        ThrowIfReleasedFromManager();
        if (_entity is not null) return;
        var nodeFactory = _nodeFactoryCreator(this);
        _entity = _world.CreateEntity(nodeFactory, _context, _logger);
    }

    private void ThrowIfReleasedFromManager()
    {
        if (_releasedFromManager)
            throw new InvalidOperationException(
                "GodotSndEntity has been released from GodotSndManager and cannot be used.");
    }
}
