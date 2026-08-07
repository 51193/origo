namespace Origo.Core.Abstractions.Node;

/// <summary>
///     Create a node instance by resource identifier and attach it to the host.
/// </summary>
public interface INodeFactory
{
    /// <summary>Creates a node instance by resource identifier and attaches it to the host.</summary>
    /// <param name="logicalName">The logical name of the node.</param>
    /// <param name="resourceId">The engine resource identifier to instantiate.</param>
    INodeHandle Create(string logicalName, string resourceId);
}
