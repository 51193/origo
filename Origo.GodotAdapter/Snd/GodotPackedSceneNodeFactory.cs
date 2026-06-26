using System;
using System.Collections.Concurrent;
using Godot;
using Origo.Core.Abstractions.Node;

namespace Origo.GodotAdapter.Snd;

public sealed class GodotPackedSceneNodeFactory : INodeFactory
{
    private readonly Node _parent;
    private readonly ConcurrentDictionary<string, PackedScene> _cache = new();

    public GodotPackedSceneNodeFactory(Node parent)
    {
        _parent = parent;
    }

    public INodeHandle Create(string logicalName, string resourceId)
    {
        var scene = _cache.GetOrAdd(resourceId, static id => ResourceLoader.Load<PackedScene>(id));
        if (scene is null)
            throw new InvalidOperationException(
                $"PackedScene not found for logicalName='{logicalName}', resourceId='{resourceId}'.");

        var node = scene.Instantiate<Node>();
        node.Name = logicalName;
        _parent.AddChild(node);
        return new GodotNodeHandle(node);
    }
}
