namespace Origo.Core.Abstractions.Node;

/// <summary>
///     Create a node instance by resource identifier and attach it to the host.
/// </summary>
public interface INodeFactory
{
    INodeHandle Create(string logicalName, string resourceId);
}
