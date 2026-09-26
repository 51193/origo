using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Runtime.Console;

namespace Origo.Core.Abstractions.Runtime;

/// <summary>
///     Stable host/runtime contract exposed to shell consumers. The concrete
///     runtime implementation remains in the kernel package.
/// </summary>
public interface IOrigoRuntime : IOrigoFrameDriver
{
    /// <summary>Framework metadata (name, version, banner).</summary>
    OrigoMeta Meta { get; }

    /// <summary>Logger service instance used throughout the runtime.</summary>
    ILogger Logger { get; }

    /// <summary>Stable SND world access surface managed by this runtime.</summary>
    ISndWorldAccess SndWorld { get; }

    /// <summary>System-level blackboard whose lifetime spans the whole application run.</summary>
    IBlackboard SystemBlackboard { get; }

    /// <summary>Console input queue, or <c>null</c> when the host did not inject one.</summary>
    IConsoleInputSource? ConsoleInput { get; }

    /// <summary>Console output channel, or <c>null</c> when the host did not inject one.</summary>
    IConsoleOutputChannel? ConsoleOutputChannel { get; }

    /// <summary>The current session manager.</summary>
    ISessionManager SessionManager { get; }

    /// <summary>
    ///     Registers a console command handler with the runtime console router.
    ///     Both console input and output channels must be supplied when the host
    ///     is created; otherwise registration fails fast instead of silently
    ///     dropping the handler.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the host was created without both console channels.
    /// </exception>
    void RegisterConsoleCommandHandler(IConsoleCommandHandler handler);
}
