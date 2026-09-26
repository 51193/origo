using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Runtime.Console;

namespace Origo.GodotAdapter.Console;

/// <summary>
///     Base class for Godot-specific console command handlers.
///     Holds a reference to the stable <see cref="IOrigoRuntime" /> and provides
///     entity lookup helpers for Godot adapter-layer commands.
///     Argument-count validation and error messaging come from
///     <see cref="ConsoleCommandHandlerBase" />.
/// </summary>
public abstract class CommandHandlerBase : ConsoleCommandHandlerBase
{
    /// <summary>Creates a handler holding the given runtime reference.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="runtime" /> is null.</exception>
    protected CommandHandlerBase(IOrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    /// <summary>The runtime the handler operates against.</summary>
    protected IOrigoRuntime Runtime { get; }
}
