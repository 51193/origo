using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter;

/// <summary>
///     Adapter-layer convenience extensions for retrieving native Godot nodes
///     from SND node handles and entities.
/// </summary>
public static class SndEntityNodeExtensions
{
    /// <summary>
    ///     Gets the native Godot node from an SND node handle (adapter layer only).
    ///     Only works when INodeHandle is a GodotNodeHandle; otherwise returns null.
    /// </summary>
    public static Node? GetNativeNode(this INodeHandle handle)
    {
        if (handle is GodotNodeHandle godotHandle)
            return godotHandle.UnsafeGetNode();
        return null;
    }

    /// <summary>
    ///     Godot adapter layer convenience method: resolves the node registered
    ///     under the given logical name in the entity's SND node registry and
    ///     casts it to the specified type. Throws when no node is registered
    ///     under that name (the SND node registry lookup is strict); only
    ///     works when the entity is a <see cref="GodotSndEntity" />.
    /// </summary>
    public static TNode? GetNodeFromSnd<TNode>(this ISndEntity entity, string name) where TNode : Node
    {
        if (entity is GodotSndEntity godotEntity)
            return godotEntity.GetNodeFromSnd<TNode>(name);
        return null;
    }
}
