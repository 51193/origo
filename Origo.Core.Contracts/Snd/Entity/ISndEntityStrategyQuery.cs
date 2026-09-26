using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Entity;

/// <summary>
///     Internal query surface used by strategy base classes to detect that a
///     strategy is already mounted without depending on the concrete entity
///     implementation.
/// </summary>
internal interface ISndEntityStrategyQuery : ISndEntity
{
    bool HasStrategyMounted(string index);
}
