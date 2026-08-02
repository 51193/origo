using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     Entity lifecycle management interface.
///     Used internally by the framework to orchestrate two-phase batch processing
///     for entity creation / loading / saving / destruction.
///     Business code must not call this interface directly.
///     <para>
///         Implementors: <see cref="Origo.Core.Snd.Entity.SndEntity" /> (Core in-memory entity),
///         adapter-layer entities (delegating to an inner SndEntity).
///         Adapter and test projects access it via <c>InternalsVisibleTo</c>.
///     </para>
/// </summary>
internal interface IEntityLifecycle
{
    /// <summary>
    ///     Phase 1: Recover entity data, nodes, and all strategies
    ///     (EntityStrategy + ActiveStrategy) from metadata without firing hooks.
    /// </summary>
    void RecoverForLifecycle(SndMetaData metaData);

    /// <summary>
    ///     Phase 2: Fire AfterSpawn hooks on entity strategies.
    /// </summary>
    void FireAfterSpawnHooks();

    /// <summary>
    ///     Phase 2: Fire AfterLoad hooks on entity strategies.
    /// </summary>
    void FireAfterLoadHooks();

    /// <summary>
    ///     Phase 2: Fire BeforeSave hooks on entity strategies.
    /// </summary>
    void FireBeforeSaveHooks();

    /// <summary>
    ///     Phase 2: Fire BeforeQuit hooks on entity strategies.
    /// </summary>
    void FireBeforeQuitHooks();

    /// <summary>
    ///     Phase 2: Fire BeforeDead hooks on entity strategies.
    /// </summary>
    void FireBeforeDeadHooks();

    /// <summary>
    ///     Phase 3: Release entity strategy and active strategy references only,
    ///     without firing hooks.
    /// </summary>
    void ReleaseStrategiesOnly();

    /// <summary>
    ///     Phase 3: Release node and data resources only, without firing hooks.
    /// </summary>
    void TeardownOnly();

    /// <summary>
    ///     Build entity metadata without firing BeforeSave hooks.
    /// </summary>
    SndMetaData BuildMetaData();
}
