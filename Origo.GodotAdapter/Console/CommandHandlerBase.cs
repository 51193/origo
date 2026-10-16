using System;
using System.Diagnostics.CodeAnalysis;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Snd;

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

    /// <summary>
    ///     Resolves a foreground entity by name and narrows it to the Godot
    ///     adapter entity that owns the engine node tree. The helper is
    ///     restricted to this assembly because the Godot entity type is an
    ///     implementation detail of the adapter shell package.
    /// </summary>
    /// <param name="entityName">Entity name to look up in the foreground session.</param>
    /// <param name="godotEntity">The resolved Godot entity when the method returns true.</param>
    /// <param name="errorMessage">Failure message when the method returns false.</param>
    /// <returns>True when the entity exists and is a Godot entity.</returns>
    private protected bool TryResolveGodotEntity(
        string entityName,
        [NotNullWhen(true)] out GodotSndEntity? godotEntity,
        out string? errorMessage)
    {
        var entity = Runtime.SessionManager.ForegroundSession?.FindByName(entityName);
        if (entity is null)
        {
            godotEntity = null;
            errorMessage = $"Entity '{entityName}' not found.";
            return false;
        }

        if (entity is not GodotSndEntity resolved)
        {
            godotEntity = null;
            errorMessage = $"Entity '{entityName}' is not a Godot entity.";
            return false;
        }

        godotEntity = resolved;
        errorMessage = null;
        return true;
    }
}
