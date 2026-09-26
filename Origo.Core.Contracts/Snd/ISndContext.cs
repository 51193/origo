using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Snd;

/// <summary>
///     Unified business facade interface for strategies and the game layer.
///     Does not inherit any role interfaces; all capabilities are accessed
///     through typed companion properties.
///     <para>
///         Companion properties by responsibility:
///         <see cref="Blackboard" /> (blackboard access),
///         <see cref="Deferred" /> (deferred action queue),
///         <see cref="Template" /> (template cloning),
///         <see cref="ConsoleAccess" /> (console I/O),
///         <see cref="StateMachines" /> (state machines),
///         <see cref="Save" /> (save / level operations),
///         <see cref="Lifecycle" /> (lifecycle entry points),
///         <see cref="FileAccess" /> (static resource file access),
///         <see cref="ArchiveFileAccess" /> (save-internal file access),
///         <see cref="StateMachineContext" /> (state machine context).
///         Strategy hooks receive the full <c>ISndContext ctx</c> parameter and
///         access capabilities through secondary access like <c>ctx.Blackboard.SystemBlackboard</c>.
///     </para>
/// </summary>
public interface ISndContext
{
    /// <summary>Boot sequence: strategy discovery → alias/template loading → entry save loading.</summary>
    void Bootstrap();

    /// <summary>Current save root path.</summary>
    string SaveRootPath { get; }

    /// <summary>Initial save root path.</summary>
    string InitialSaveRootPath { get; }

    /// <summary>Entry configuration path.</summary>
    string EntryConfigPath { get; }

    /// <summary>System-level and progress-level blackboard access.</summary>
    ISndBlackboardAccess Blackboard { get; }

    /// <summary>Deferred action queue.</summary>
    ISndDeferredActions Deferred { get; }

    /// <summary>Template cloning.</summary>
    ISndTemplateAccess Template { get; }

    /// <summary>Console command submission and output subscription.</summary>
    ISndConsoleAccess ConsoleAccess { get; }

    /// <summary>Progress-level state machine container access.</summary>
    ISndStateMachineAccess StateMachines { get; }

    /// <summary>Save read/write and level switching.</summary>
    ISndSaveOperations Save { get; }

    /// <summary>Save lifecycle entry points: continue game, initial save, main menu entry.</summary>
    ISndLifecycleOperations Lifecycle { get; }

    /// <summary>Static resource file access (strategy scope, paths relative to project config directory).</summary>
    ISndFileAccess FileAccess { get; }

    /// <summary>Save-internal file access (paths relative to the save's extra/ subdirectory).</summary>
    ISndArchiveFileAccess ArchiveFileAccess { get; }

    /// <summary>State machine context (blackboard access + deferred actions + session/scene access).</summary>
    IStateMachineContext StateMachineContext { get; }
}
