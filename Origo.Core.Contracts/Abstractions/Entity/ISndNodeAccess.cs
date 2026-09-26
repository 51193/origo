using System.Collections.Generic;
using Origo.Core.Abstractions.Node;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     Node access capability extracted from <see cref="ISndEntity" />,
///     following the interface segregation principle.
/// </summary>
public interface ISndNodeAccess
{
    /// <summary>Gets the node handle registered under the given name.</summary>
    INodeHandle GetNode(string name);

    /// <summary>Gets the names of all nodes registered on the entity.</summary>
    IReadOnlyCollection<string> GetNodeNames();
}
