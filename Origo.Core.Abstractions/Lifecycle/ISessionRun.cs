using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Lifecycle;

/// <summary>
///     Session-level runtime facade for a single level.
///     Strategies use this to access session capabilities: entity operations
///     (find / spawn / kill), the session blackboard, state machines, and the
///     owning <see cref="ISessionManager" /> (for cross-session access).
///     Lifecycle (create / destroy) and serialization are managed by <see cref="ISessionManager" />.
///     Foreground and background sessions expose the same interface; they differ
///     only by the <see cref="IsFrontSession" /> flag.
/// </summary>
public interface ISessionRun : IDisposable
{
    /// <summary>Session-scoped blackboard, isolated from other sessions.</summary>
    IBlackboard SessionBlackboard { get; }

    /// <summary>Level identifier for this session.</summary>
    string LevelId { get; }

    /// <summary>Whether this session is the active foreground session.</summary>
    bool IsFrontSession { get; }

    /// <summary>Session-level state machine container.</summary>
    IStateMachineContainer GetSessionStateMachines();

    /// <summary>
    ///     The <see cref="ISessionManager" /> that owns this session.
    ///     Strategies use this for cross-session access to other sessions.
    /// </summary>
    ISessionManager SessionManager { get; }

    // ── Entity operations (session scope) ──

    /// <summary>Find an entity by name within this session. Returns null if not found.</summary>
    ISndEntity? FindByName(string name);

    /// <summary>Get all entities currently in this session.</summary>
    IReadOnlyCollection<ISndEntity> GetEntities();

    /// <summary>Spawn a new entity from metadata into this session.</summary>
    ISndEntity Spawn(SndMetaData meta);

    /// <summary>Spawn multiple entities from metadata into this session.</summary>
    void SpawnMany(params SndMetaData[] metaList);

    /// <summary>Mark an entity for deferred removal at the end of the current frame.</summary>
    void RequestKillEntity(string entityName);
}
