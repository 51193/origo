using Godot;
using Origo.Core.Abstractions.Node;

namespace Origo.GodotAdapter.Snd;

internal sealed class GodotNodeHandle : INodeHandle
{
    private readonly Node _node;

    public GodotNodeHandle(Node node)
    {
        _node = node;
    }

    public string Name => _node.Name;

    public object Native => _node;

    public void Free() => _node.Free();

    public void SetVisible(bool visible)
    {
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
