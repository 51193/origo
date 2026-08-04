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
    protected CommandHandlerBase(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    protected OrigoRuntime Runtime { get; }
}
