using System;
using Godot;
using Origo.Core.Abstractions.Node;

namespace Origo.GodotAdapter.Snd;

/// <summary>
///     使用 Godot PackedScene 创建节点并挂载到指定父节点。
/// </summary>
public sealed class GodotPackedSceneNodeFactory : INodeFactory
{
    private readonly Node _parent;

    public GodotPackedSceneNodeFactory(Node parent)
    {
        _parent = parent;
    }

    public INodeHandle Create(string logicalName, string resourceId)
    {
        var scene = ResourceLoader.Load<PackedScene>(resourceId);
        if (scene is null)
            throw new InvalidOperationException(
                $"PackedScene not found for logicalName='{logicalName}', resourceId='{resourceId}'.");

        var node = scene.Instantiate<Node>();
        node.Name = logicalName;
        _parent.AddChild(node);
        return new GodotNodeHandle(node);
    }
}
