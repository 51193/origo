using System.Collections.Generic;
using Origo.Core.Abstractions.Node;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     Node access capability extracted from <see cref="ISndEntity" />,
///     following the interface segregation principle.
/// </summary>
public interface ISndNodeAccess
{
    INodeHandle GetNode(string name);

    IReadOnlyCollection<string> GetNodeNames();
}
