using System;
using Origo.Core.Abstractions.Node;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     纯内存 <see cref="INodeFactory" /> 实现，不依赖任何引擎适配层。
///     创建的节点句柄仅占位，不绑定引擎节点。
///     用于 <see cref="FullMemorySndSceneHost" /> 等 Core 层内存级场景（如后台关卡）。
/// </summary>
internal sealed class NullNodeFactory : INodeFactory
{
    /// <inheritdoc />
    public INodeHandle Create(string logicalName, string resourceId) => new NullNodeHandle(logicalName);
}

/// <summary>
///     纯内存 <see cref="INodeHandle" /> 实现，不绑定引擎节点。
///     所有操作均为空操作，仅用于 Core 层内存场景。
/// </summary>
internal sealed class NullNodeHandle(string name) : INodeHandle
{

    /// <inheritdoc />
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <inheritdoc />
    public void Free()
    {
        // No-op in memory.
    }

    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        // No-op in memory.
    }
}
