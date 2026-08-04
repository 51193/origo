using System;
using System.Collections.Generic;
using Godot;
using Origo.Core.Abstractions.Node;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     Creates Godot nodes by loading <c>PackedScene</c> resources.
///     Scenes are cached by resource ID to avoid redundant disk I/O.
/// </summary>
public sealed class GodotPackedSceneNodeFactory(Node parent) : INodeFactory
{
    private readonly Node _parent = parent;
    private readonly Dictionary<string, PackedScene> _cache = [];

    public INodeHandle Create(string logicalName, string resourceId)
    {
        if (!_cache.TryGetValue(resourceId, out var scene))
        {
            scene = ResourceLoader.Load<PackedScene>(resourceId)
                ?? throw new InvalidOperationException(
                    $"PackedScene not found for logicalName='{logicalName}', resourceId='{resourceId}'.");
            // Cache only successful loads; a failed resource id stays out of
            // the cache so it can be retried after the resource becomes
            // available (negative caching would pin the failure forever).
            _cache[resourceId] = scene;
        }

        var node = scene.Instantiate<Node>();
        node.Name = logicalName;
        _parent.AddChild(node);
        return new GodotNodeHandle(node);
    }
}
