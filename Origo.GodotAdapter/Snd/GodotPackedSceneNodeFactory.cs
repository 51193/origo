using System;
using System.Collections.Generic;
using Godot;
using Origo.Core.Abstractions.Node;

namespace Origo.GodotAdapter.Snd;

public sealed class GodotPackedSceneNodeFactory(Node parent) : INodeFactory
{
    private readonly Node _parent = parent;
    private readonly Dictionary<string, PackedScene> _cache = [];

    public INodeHandle Create(string logicalName, string resourceId)
    {
        if (!_cache.TryGetValue(resourceId, out var scene))
        {
            scene = ResourceLoader.Load<PackedScene>(resourceId);
            _cache[resourceId] = scene;
        }

        if (scene is null)
            throw new InvalidOperationException(
                $"PackedScene not found for logicalName='{logicalName}', resourceId='{resourceId}'.");
        var node = scene.Instantiate<Node>();
        node.Name = logicalName;
        _parent.AddChild(node);
        return new GodotNodeHandle(node);
    }
}
