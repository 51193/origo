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
public interface ISndSceneHost : ISndSceneAccess
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
    ///         Note: this method does not enforce name uniqueness checks;
    ///         the framework currently does not mandate unique names at the
    ///         upper layer either.
    ///     </para>
    /// </summary>
    ISndEntity CreateEntity(SndMetaData metaData);

    /// <summary>
    ///     Get a view of all currently alive entities in the scene.
    /// </summary>
    IReadOnlyCollection<ISndEntity> GetEntities();

    /// <summary>
    ///     Look up an entity by its name.
    /// </summary>
    ISndEntity? FindByName(string name);

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
    ///     Remove a single entity by name (only removes from the collection
    ///     and releases engine resources; does not trigger hooks or release
    ///     strategies). Hooks and strategy release are executed in batch by
    ///     the framework before calling this method. Only called internally
    ///     by the framework during lifecycle transitions.
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
