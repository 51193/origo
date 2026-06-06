using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Snd;

public static class SndEntityNodeExtensions
{
    /// <summary>
    ///     从 SND 节点句柄获取原生 Godot 节点（适配器层专用）。
    ///     仅在 INodeHandle 是 GodotNodeHandle 时生效；否则返回 null。
    /// </summary>
    public static Node? GetNativeNode(this INodeHandle handle)
    {
        if (handle is GodotNodeHandle godotHandle)
            return godotHandle.UnsafeGetNode();
        return null;
    }

    /// <summary>
    ///     Godot 适配层便利方法：遍历 Godot 场景树按名称查找节点并转换为指定类型。
    ///     仅在 entity 是 GodotSndEntity 时生效。
    /// </summary>
    public static TNode? GetNodeFromSnd<TNode>(this ISndEntity entity, string name) where TNode : Node
    {
        if (entity is GodotSndEntity godotEntity)
            return godotEntity.GetNodeFromSnd<TNode>(name);
        return null;
    }
}
