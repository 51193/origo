using System.Collections.Generic;
using Origo.Core.Abstractions.Node;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     节点访问能力，从 <see cref="ISndEntity" /> 中拆分，遵循接口隔离原则。
/// </summary>
public interface ISndNodeAccess
{
    INodeHandle GetNode(string name);

    IReadOnlyCollection<string> GetNodeNames();
}
