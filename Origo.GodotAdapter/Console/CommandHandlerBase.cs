using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;

namespace Origo.GodotAdapter.Console;

/// <summary>
///     Base class for Godot-specific console command handlers.
///     Holds a reference to <see cref="OrigoRuntime" /> and provides
///     entity lookup helpers for Godot adapter-layer commands.
///     Argument-count validation and error messaging come from
///     <see cref="ConsoleCommandHandlerBase" />.
/// </summary>
public abstract class CommandHandlerBase : ConsoleCommandHandlerBase
{
    /// <summary>Creates a handler holding the given runtime reference.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="runtime" /> is null.</exception>
    protected CommandHandlerBase(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    /// <summary>The runtime the handler operates against.</summary>
    protected OrigoRuntime Runtime { get; }
}
