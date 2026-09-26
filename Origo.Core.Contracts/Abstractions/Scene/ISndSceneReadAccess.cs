using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     Read-only scene access exposed to business-facing contexts such as
///     state-machine hooks and save-meta contributors. It allows querying the
///     current session's entities but provides no mutation, serialization, or
///     recovery operations; those framework orchestration paths are internal.
/// </summary>
public interface ISndSceneReadAccess
{
    /// <summary>Gets a snapshot of all currently alive entities in the scene.</summary>
    IReadOnlyCollection<ISndEntity> GetEntities();

    /// <summary>Looks up an entity by its stable name; returns null when not found.</summary>
    ISndEntity? FindByName(string name);
}
