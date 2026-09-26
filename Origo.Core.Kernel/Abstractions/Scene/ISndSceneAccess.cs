using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     Abstract SND scene access capability for Core-layer save/load
///     orchestration. Only responsible for data transformation
///     (serialization/restoration of metadata lists); does not trigger
///     strategy lifecycle hooks. Hook triggering is orchestrated by
///     upper layers
///     (<see cref="Origo.Core.Snd.Scene.SndEntityFactory" /> /
///     <see cref="Origo.Core.Runtime.Lifecycle.SessionRun" />)
///     before and after calls.
/// </summary>
internal interface ISndSceneAccess
{
    /// <summary>
    ///     Collect a metadata list of all entities in the current scene.
    ///     Does not trigger BeforeSave hooks — hooks should be triggered
    ///     in batch by the caller before calling this method.
    /// </summary>
    IReadOnlyList<SndMetaData> BuildMetaList();

    /// <summary>
    ///     Restore all entities (data, nodes, strategies) from a metadata list.
    ///     Does not trigger AfterLoad hooks — hooks should be triggered
    ///     in batch by the caller after calling this method.
    ///     Does not automatically clear existing entities — the caller
    ///     should handle old entity cleanup before calling this method.
    /// </summary>
    void RecoverFromMetaList(IEnumerable<SndMetaData> metaList);
}
