using Origo.Core.Runtime.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Serialization;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Persistence partial for <see cref="ProgressRun" />: save coordination
///     (build metadata, build payload, persist progress). Delegates to
///     <see cref="SaveCoordinator" />.
/// </summary>
internal sealed partial class ProgressRun
{
    internal SaveMetaBuildContext BuildSaveMetaContext(string saveId) => _saveCoordinator.BuildSaveMetaContext(saveId);

    internal SaveGamePayload BuildSavePayload(
        string newSaveId,
        IReadOnlyDictionary<string, string>? mergedMeta = null) =>
        _saveCoordinator.BuildSavePayload(newSaveId, mergedMeta);

    internal void PersistProgress() => _saveCoordinator.PersistProgress();
}
