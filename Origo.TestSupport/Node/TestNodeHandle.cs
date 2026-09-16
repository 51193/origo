using Origo.Core.Abstractions.Node;

namespace Origo.TestSupport;

/// <summary>
///     Test <see cref="INodeHandle" /> with in-memory visibility and an
///     observable free count.
/// </summary>
public sealed class TestNodeHandle(string name) : INodeHandle
{
    /// <summary>Current visibility as set through <see cref="SetVisible" />.</summary>
    public bool IsVisible { get; private set; } = true;

    /// <summary>Number of times <see cref="Free" /> has been called.</summary>
    public int FreeCount { get; private set; }

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public void Free() => FreeCount++;

    /// <inheritdoc/>
    public void SetVisible(bool visible) => IsVisible = visible;
}
