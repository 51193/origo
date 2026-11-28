using System;
using System.Runtime.CompilerServices;
using Origo.Core.Snd;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Per-SND-context gameplay services. Replaces the process-global gameplay runtime for demo code.</summary>
public sealed class EvolutionGameSession
{
    private static readonly ConditionalWeakTable<ISndContext, EvolutionGameSession> Sessions = [];
    private static readonly ConditionalWeakTable<Origo.Core.Abstractions.Lifecycle.ISessionRun, EvolutionGameSession> RunSessions = [];

    public EvolutionGameSession(ISndContext context, GameplayRuntime runtime)
    {
        Context = context;
        Runtime = runtime;
    }

    public ISndContext Context { get; }
    public GameplayRuntime Runtime { get; }
    internal SimulationProfiler Profiler { get; } = SimulationProfiler.CreateFromEnvironment();

    /// <summary>
    ///     Registers after the SND context is constructed. Frame-time strategies
    ///     resolve the session lazily; bootstrap-time hooks must not require it.
    /// </summary>
    public static void Register(EvolutionGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Sessions.AddOrUpdate(session.Context, session);
    }

    /// <summary>Binds one box session to its gameplay owner.</summary>
    public static void RegisterSession(Origo.Core.Abstractions.Lifecycle.ISessionRun run, EvolutionGameSession session)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(session);
        RunSessions.AddOrUpdate(run, session);
    }

    public static EvolutionGameSession ForSession(Origo.Core.Abstractions.Lifecycle.ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return RunSessions.TryGetValue(session, out var value)
            ? value
            : throw new InvalidOperationException($"No evolution gameplay session is registered for level '{session.LevelId}'.");
    }

    public static EvolutionGameSession For(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Sessions.TryGetValue(context, out var session)
            ? session
            : throw new InvalidOperationException("No evolution gameplay session is registered for this SND context.");
    }
}
