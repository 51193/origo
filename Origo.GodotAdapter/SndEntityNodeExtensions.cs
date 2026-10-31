using System;
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle" /> is null.</exception>
    public static Node? GetNativeNode(this INodeHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (handle is GodotNodeHandle godotHandle)
            return godotHandle.UnsafeGetNode();
        return null;
    }

    /// <summary>
    ///     Godot adapter layer convenience method: resolves the node registered
    ///     under the given logical name in the entity's SND node registry and
    ///     casts it to the specified type. Strategies receive the inner SND
    ///     entity, so the registry lookup is the authoritative path; the adapter
    ///     wrapper is accepted as well. Returns null when the registered handle
    ///     is not a Godot handle (for example in a background session), matching
    ///     <see cref="GetNativeNode" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity" /> is null.</exception>
    public static TNode? GetNodeFromSnd<TNode>(this ISndEntity entity, string name) where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity is GodotSndEntity godotEntity)
            return godotEntity.GetNodeFromSnd<TNode>(name);

        return entity.GetNode(name).GetNativeNode() as TNode;
    }
}
