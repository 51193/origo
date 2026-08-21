using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     Core-facing SND scene host abstraction.
///     Only responsible for entity container management (create/find/remove);
///     does not handle any strategy lifecycle hooks.
///     All hook orchestration is handled uniformly by the session lifecycle
///     (<see cref="Origo.Core.Runtime.Lifecycle.SessionRun" /> and
///     <see cref="Origo.Core.Runtime.Lifecycle.SessionManager" />).
///     <para>
///         Adapter layer implementations of this interface must not trigger
///         any strategy hooks — hooks are the exclusive responsibility of
///         the Core layer.
///     </para>
/// </summary>
internal interface ISndSceneHost : ISndSceneAccess, ISndSceneReadAccess
{
    /// <summary>
    ///     Create an entity in the scene from metadata, restoring data,
    ///     strategies, and nodes into memory.
    ///     Does not trigger any lifecycle hooks (AfterSpawn, AfterLoad, etc.).
    ///     Hooks should be triggered by the caller
    ///     (<see cref="Origo.Core.Snd.Scene.SndEntityFactory" /> /
    ///     <see cref="Origo.Core.Runtime.Lifecycle.SessionRun" />)
    ///     at the appropriate phase.
    ///     <para>
    ///         Name uniqueness is enforced by the orchestration layers before
    ///         this method is called (<see cref="Origo.Core.Snd.Scene.SndEntityFactory" />
    ///         for spawn, <see cref="Origo.Core.Save.Serialization.SndSceneSerializer" />
    ///         for load); hosts may assume names are unique within the scene.
    ///     </para>
    /// </summary>
    ISndEntity CreateEntity(SndMetaData metaData);

    /// <summary>
    ///     Execute per-frame update for all alive entities.
    ///     Host implementations that do not support frame updates should be
    ///     no-ops. This method is only responsible for dispatching frame
    ///     updates per entity; it does not handle global pipeline
    ///     orchestration such as deferred action flushing.
    /// </summary>
    /// <param name="delta">Frame interval in seconds.</param>
    void ProcessAll(double delta);

    /// <summary>
    ///     Immediately mark the specified entity as pending destruction.
    ///     The entity is destroyed at frame-end (after the business deferred
    ///     queue, before the system deferred queue).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     If the entity does not exist or is already marked as pending
    ///     destruction.
    /// </exception>
    void RequestKillEntity(string name);

    /// <summary>
    ///     Remove a single entity by name (only removes the entity from the
    ///     collection; does not trigger hooks, release strategies, or release
    ///     engine resources). Hook triggering and strategy release are
    ///     executed in batch by the framework before calling this method.
    ///     Only called internally by the framework during lifecycle
    ///     transitions.
    /// </summary>
    void RemoveEntity(string name);

    /// <summary>
    ///     Clear all entity collection references in the scene.
    ///     Hooks and strategy release should be executed in batch by the
    ///     caller before calling this method. Only called internally by the
    ///     framework during lifecycle transitions.
    /// </summary>
    void RemoveAllEntities();
}
