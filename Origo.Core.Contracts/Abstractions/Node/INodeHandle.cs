namespace Origo.Core.Abstractions.Node;

/// <summary>
///     Abstract engine node handle through which Core triggers basic
///     node behavior.
/// </summary>
public interface INodeHandle
{
    /// <summary>The logical name of the wrapped node.</summary>
    string Name { get; }

    /// <summary>Releases the underlying engine node.</summary>
    void Free();

    /// <summary>Sets the node's visibility in the engine scene.</summary>
    /// <param name="visible">Whether the node should be visible.</param>
    void SetVisible(bool visible);
}
