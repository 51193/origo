using Godot;
using Origo.Core.Abstractions.Node;

namespace Origo.GodotAdapter.Snd;

internal sealed class GodotNodeHandle(Node node) : INodeHandle
{
    private readonly Node _node = node;
    private readonly string _cachedName = node.Name;

    public string Name => _cachedName;

    public void Free()
    {
        if (GodotObject.IsInstanceValid(_node))
            _node.Free();
    }

    /// <summary>
    ///     Returns the raw Godot <see cref="Node" /> reference.
    ///     This bypasses the <see cref="INodeHandle" /> abstraction and should be used
    ///     only in tightly controlled adapter-internal contexts where direct node access
    ///     is required (e.g., <see cref="GodotSndEntity.GetNodeFromSnd{TNode}" />).
    /// </summary>
    internal Node UnsafeGetNode() => _node;

    public void SetVisible(bool visible)
    {
        if (!GodotObject.IsInstanceValid(_node))
            return;

        switch (_node)
        {
            case CanvasItem canvasItem:
                canvasItem.Visible = visible;
                break;
            case Node3D node3D:
                node3D.Visible = visible;
                break;
        }
    }
}
