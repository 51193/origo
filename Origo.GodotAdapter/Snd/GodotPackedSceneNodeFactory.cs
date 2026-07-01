using System;
using System.Collections.Concurrent;
using Godot;
using Origo.Core.Abstractions.Node;

namespace Origo.GodotAdapter.Snd;

public sealed class GodotPackedSceneNodeFactory(Node parent) : INodeFactory
{
    private readonly Node _parent = parent;
    private readonly ConcurrentDictionary<string, PackedScene> _cache = new();

    public INodeHandle Create(string logicalName, string resourceId)
    {
        var scene = _cache.GetOrAdd(resourceId, static id => ResourceLoader.Load<PackedScene>(id)) ?? throw new InvalidOperationException(
                $"PackedScene not found for logicalName='{logicalName}', resourceId='{resourceId}'.");
        var node = scene.Instantiate<Node>();
        node.Name = logicalName;
        _parent.AddChild(node);
        return new GodotNodeHandle(node);
    }
}
