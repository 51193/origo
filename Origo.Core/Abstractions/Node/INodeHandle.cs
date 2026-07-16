namespace Origo.Core.Abstractions.Node;

/// <summary>
///     Abstract engine node handle through which Core triggers basic
///     node behavior.
/// </summary>
public interface INodeHandle
{
    string Name { get; }

    void Free();

    void SetVisible(bool visible);
}
