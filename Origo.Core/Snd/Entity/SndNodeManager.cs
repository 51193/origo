using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Logging;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     与引擎无关的节点元数据恢复器，具体节点创建由 INodeFactory 提供。
/// </summary>
internal sealed class SndNodeManager : INodeHost
{
    private readonly INodeFactory _factory;
    private readonly ILogger _logger;
    private readonly Dictionary<string, INodeHandle> _nodes = new();
    private Dictionary<string, string> _resources = new();
    private Func<string, string> _resolveSceneAlias = static s => s;

    public SndNodeManager(INodeFactory factory, ILogger logger)
    {
        _factory = factory;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void SetSceneAliasResolver(Func<string, string> resolveSceneAlias)
    {
        _resolveSceneAlias = resolveSceneAlias ?? (static s => s);
    }

    public INodeHandle GetNode(string name)
    {
        if (_nodes.TryGetValue(name, out var node)) return node;

        _logger.Log(LogLevel.Error, nameof(SndNodeManager),
            new LogMessageBuilder().AddContext("nodeName", name).Build("Node not found."));
        throw new InvalidOperationException($"Node '{name}' not found.");
    }

    public IReadOnlyCollection<string> GetNodeNames() => _nodes.Keys;

    public void Recover(NodeMetaData metaData)
    {
        Release();
        _resources = new Dictionary<string, string>(metaData.Pairs);

        foreach (var pair in _resources)
        {
            var resourceId = _resolveSceneAlias(pair.Value);
            try
            {
                _nodes[pair.Key] = _factory.Create(pair.Key, resourceId);
            }
            catch (Exception ex)
            {
                Release();
                throw new InvalidOperationException(
                    $"Failed to create node logicalName='{pair.Key}', resourceId='{resourceId}'.", ex);
            }
        }

        _logger.Log(LogLevel.Info, nameof(SndNodeManager),
            new LogMessageBuilder().Build($"Loaded {_nodes.Count} nodes."));
    }

    public void Release()
    {
        foreach (var node in _nodes.Values) node.Free();

        _nodes.Clear();
        _resources.Clear();
    }

    public NodeMetaData SerializeMetaData()
    {
        return new NodeMetaData
        {
            Pairs = new Dictionary<string, string>(_resources)
        };
    }
}
