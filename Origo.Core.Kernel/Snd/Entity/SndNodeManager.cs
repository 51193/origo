using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Node;
using Origo.Core.Logging;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     Engine-agnostic node metadata restorer;
///     actual node creation is provided by INodeFactory.
/// </summary>
internal sealed class SndNodeManager : INodeHost
{
    private readonly INodeFactory _factory;
    private readonly ILogger _logger;
    private readonly Dictionary<string, INodeHandle> _nodes = [];
    private Dictionary<string, string> _resources = [];
    private Func<string, string> _resolveSceneAlias = static s => s;

    public SndNodeManager(INodeFactory factory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void SetSceneAliasResolver(Func<string, string> resolveSceneAlias)
    {
        ArgumentNullException.ThrowIfNull(resolveSceneAlias);
        _resolveSceneAlias = resolveSceneAlias;
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
                _logger.Log(LogLevel.Warning, nameof(SndNodeManager),
                    new LogMessageBuilder()
                        .AddContext("logicalName", pair.Key)
                        .AddContext("resourceId", resourceId)
                        .Build($"Node creation failed: {ex.Message}"));
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
        // A throwing node release must not skip the remaining nodes or leave
        // the dictionaries in a half-cleared state: release every node, then
        // clear regardless of individual failures (the first failure still
        // propagates, fail-fast).
        Exception? firstFailure = null;
        foreach (var node in _nodes.Values)
        {
            try
            {
                node.Free();
            }
            catch (Exception ex)
            {
                firstFailure ??= ex;
                _logger.Log(LogLevel.Warning, nameof(SndNodeManager),
                    new LogMessageBuilder().Build($"Node release failed: {ex.Message}"));
            }
        }

        _nodes.Clear();
        _resources.Clear();

        if (firstFailure is not null)
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }

    public NodeMetaData SerializeMetaData()
    {
        return new NodeMetaData
        {
            Pairs = new Dictionary<string, string>(_resources)
        };
    }
}
