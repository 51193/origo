using System;
using Origo.Core.Abstractions.Node;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     Pure in-memory <see cref="INodeFactory" /> implementation that does not depend
///     on any engine adapter layer. Created node handles are placeholders only
///     and do not bind to engine nodes.
///     Used by Core-layer in-memory scenes such as <see cref="FullMemorySndSceneHost" />
///     (e.g., background levels).
/// </summary>
internal sealed class NullNodeFactory : INodeFactory
{
    /// <inheritdoc />
    public INodeHandle Create(string logicalName, string resourceId) => new NullNodeHandle(logicalName);
}

/// <summary>
///     Pure in-memory <see cref="INodeHandle" /> implementation that does not bind
///     to engine nodes. All operations are no-ops, used only for Core-layer in-memory scenes.
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
