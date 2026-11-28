using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Per-context gameplay host owning all ecology boxes.</summary>
public sealed class EvolutionGameSession
{
    private static readonly ConditionalWeakTable<ISndContext, EvolutionGameSession> Sessions = [];
    private static readonly ConditionalWeakTable<ISessionRun, EvolutionGameSession> RunSessions = [];
    private readonly Func<ISessionManager> _sessionsProvider;
    private readonly BoxUnlockProfile _defaultProfile;
    private readonly List<EcologyBox> _boxes = [];
    private EcologyBox? _foreground;

    /// <summary>Creates a host bound to the live session manager of its context.</summary>
    /// <param name="context">Owning SND context.</param>
    /// <param name="sessionsProvider">Returns the current progress run's session manager.</param>
    public EvolutionGameSession(ISndContext context, Func<ISessionManager> sessionsProvider)
    {
        Context = context;
        _sessionsProvider = sessionsProvider;
        var content = GameplayRuntime.CreateDefault([new CoreEvolutionModule()], GameplayMode.Profile).Content;
        _defaultProfile = BoxUnlockProfile.All(content);
    }

    /// <summary>Owning SND context.</summary>
    public ISndContext Context { get; }
    private ISessionManager LiveSessions => _sessionsProvider();
    /// <summary>All registered boxes, including loaded background sessions.</summary>
    public IReadOnlyList<EcologyBox> Boxes => _boxes;
    /// <summary>Foreground box resolved from the current session manager.</summary>
    public EcologyBox ForegroundBox => EnsureForegroundBox();
    /// <summary>Foreground box runtime used by the visible UI.</summary>
    public GameplayRuntime Runtime => _foreground?.Runtime
        ?? throw new InvalidOperationException("No foreground ecology box is registered.");
    internal SimulationProfiler Profiler { get; } = SimulationProfiler.CreateFromEnvironment();

    /// <summary>Registers a host for frame-time lookup by context.</summary>
    /// <param name="session">Host to register.</param>
    public static void Register(EvolutionGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Sessions.AddOrUpdate(session.Context, session);
    }

    /// <summary>Binds a session to its owning box host.</summary>
    /// <param name="run">Mounted session.</param>
    /// <param name="session">Owning host.</param>
    public static void RegisterSession(ISessionRun run, EvolutionGameSession session)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(session);
        RunSessions.AddOrUpdate(run, session);
    }

    /// <summary>Resolves the host registered for a context.</summary>
    /// <param name="context">Owning SND context.</param>
    public static EvolutionGameSession For(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Sessions.TryGetValue(context, out var session)
            ? session
            : throw new InvalidOperationException("No evolution gameplay session is registered for this SND context.");
    }

    /// <summary>Resolves the host registered for a session.</summary>
    /// <param name="session">Mounted session.</param>
    public static EvolutionGameSession ForSession(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return RunSessions.TryGetValue(session, out var value)
            ? value
            : throw new InvalidOperationException($"No evolution gameplay session is registered for level '{session.LevelId}'.");
    }

    /// <summary>Creates the foreground box runtime around an existing controller session.</summary>
    /// <param name="profile">Box unlock profile.</param>
    /// <param name="seed">Simulation seed.</param>
    /// <param name="session">Foreground session.</param>
    public void RegisterForeground(BoxUnlockProfile profile, int seed, ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(session);
        var runtime = GameplayRuntime.CreateForBox(profile, [new CoreEvolutionModule()]);
        var box = new EcologyBox("pond", session.LevelId, profile, runtime, session, seed);
        RegisterBox(box, foreground: true);
        if (box.Controller is { } controller)
        {
            GameplayStateSync.Save(controller, runtime);
            BoxProfileCodec.Save(session, box);
        }
    }

    /// <summary>Rebuilds a box from a loaded session profile.</summary>
    /// <param name="session">Loaded box session.</param>
    public EcologyBox AttachLoadedBox(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!BoxProfileCodec.TryRead(session, out var id, out var profile, out var seed))
        {
            if (!session.IsFrontSession)
                throw new InvalidOperationException($"Session '{session.LevelId}' has no valid box profile.");
            id = "pond";
            profile = _defaultProfile;
            seed = session.FindByName(DemoEntry.ControllerName)?.TryGetData<int>(DemoKeys.Seed) is { found: true, value: var value }
                ? value : 0;
        }

        var runtime = GameplayRuntime.CreateForBox(profile, [new CoreEvolutionModule()]);
        var box = new EcologyBox(id, session.LevelId, profile, runtime, session, seed);
        RegisterBox(box, foreground: session.IsFrontSession);
        if (box.Controller is { } controller)
        {
            GameplayStateSync.Save(controller, runtime);
            BoxProfileCodec.Save(session, box);
        }

        return box;
    }

    /// <summary>Creates and mounts a background ecology session.</summary>
    /// <param name="id">Stable box id.</param>
    /// <param name="profile">Box unlock profile.</param>
    /// <param name="seed">Simulation seed.</param>
    public EcologyBox CreateBackgroundBox(string id, BoxUnlockProfile profile, int seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(profile);
        var levelId = "box_" + id.ToLowerInvariant();
        if (LiveSessions.TryGet(levelId) is not null)
            throw new InvalidOperationException($"Box session '{levelId}' already exists.");
        var session = LiveSessions.CreateBackgroundSession(levelId, levelId, syncProcess: true);
        var runtime = GameplayRuntime.CreateForBox(profile, [new CoreEvolutionModule()]);
        var box = new EcologyBox(id, levelId, profile, runtime, session, seed);
        RegisterBox(box, foreground: false);
        var controller = session.Spawn(EvolutionEntityFactory.Controller(seed));
        GameplayStateSync.Save(controller, runtime);
        BoxProfileCodec.Save(session, box);
        return box;
    }

    /// <summary>Attaches live box sessions and restores the default background box.</summary>
    public void EnsureTopology()
    {
        if (LiveSessions.ForegroundSession is null)
            return;
        _ = EnsureForegroundBox();
        foreach (var key in LiveSessions.Keys)
        {
            var run = LiveSessions.TryGet(key);
            if (run is null || run.IsFrontSession || TryGetBox(run) is not null)
                continue;
            AttachLoadedBox(run);
        }

        var hasLiveBackground = _boxes.Any(box =>
            !box.IsForeground && ReferenceEquals(LiveSessions.TryGet(box.LevelId), box.Session));
        if (!hasLiveBackground)
            CreateBackgroundBox("meadow", _defaultProfile, ForegroundBox.Seed + 1);
    }

    /// <summary>Finds a box registered for a session.</summary>
    /// <param name="session">Mounted session.</param>
    public EcologyBox? TryGetBox(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _boxes.FirstOrDefault(box => ReferenceEquals(box.Session, session));
    }

    /// <summary>Resolves a box, rebuilding it from a loaded session when needed.</summary>
    /// <param name="session">Mounted session.</param>
    public EcologyBox BoxFor(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return TryGetBox(session) ?? AttachLoadedBox(session);
    }

    private EcologyBox EnsureForegroundBox()
    {
        var foreground = LiveSessions.ForegroundSession
            ?? throw new InvalidOperationException("The foreground session is not mounted.");
        var box = BoxFor(foreground);
        _foreground = box;
        return box;
    }

    private void RegisterBox(EcologyBox box, bool foreground)
    {
        _boxes.RemoveAll(existing => string.Equals(existing.LevelId, box.LevelId, StringComparison.Ordinal));
        _boxes.Add(box);
        if (foreground)
            _foreground = box;
        RegisterSession(box.Session, this);
    }
}
