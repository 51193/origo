using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>One independently profiled ecology simulation mounted as a session.</summary>
public sealed class EcologyBox
{
    /// <summary>Creates a box bound to its session.</summary>
    /// <param name="id">Stable box id.</param>
    /// <param name="levelId">Session level id.</param>
    /// <param name="profile">Box unlock profile.</param>
    /// <param name="runtime">Box-owned gameplay runtime.</param>
    /// <param name="session">Mounted session.</param>
    /// <param name="seed">Simulation seed.</param>
    public EcologyBox(
        string id,
        string levelId,
        BoxUnlockProfile profile,
        GameplayRuntime runtime,
        ISessionRun session,
        int seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(levelId);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(session);
        Id = id;
        LevelId = levelId;
        Profile = profile;
        Runtime = runtime;
        Session = session;
        Seed = seed;
    }

    /// <summary>Stable box id.</summary>
    public string Id { get; }

    /// <summary>Session level id.</summary>
    public string LevelId { get; }

    /// <summary>Box unlock profile.</summary>
    public BoxUnlockProfile Profile { get; }

    /// <summary>Box-owned gameplay runtime.</summary>
    public GameplayRuntime Runtime { get; }

    /// <summary>Mounted session.</summary>
    public ISessionRun Session { get; }

    /// <summary>Simulation seed.</summary>
    public int Seed { get; }

    /// <summary>Whether this box is the visible foreground session.</summary>
    public bool IsForeground => Session.IsFrontSession;

    /// <summary>Box world controller, when spawned.</summary>
    public ISndEntity? Controller => Session.FindByName(DemoEntry.ControllerName);
}
