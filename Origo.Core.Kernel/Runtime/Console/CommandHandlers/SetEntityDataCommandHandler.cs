using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>entity_set_data</c> command: sets SND entity data by name, key, and value, auto-infers type and preserves the type of existing keys.
///     Usage: <c>entity_set_data &lt;entity&gt; &lt;key&gt; &lt;value&gt;</c>
/// </summary>
internal sealed class SetEntityDataCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public SetEntityDataCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "entity_set_data";
    public override string HelpText => "entity_set_data <entity> <key> <value> — set entity data (type inferred automatically; existing keys keep their type).";
    public override int MinPositionalArgs => 3;
    public override int MaxPositionalArgs => 3;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();
        var key = invocation.PositionalArgs[1].Trim();
        var raw = invocation.PositionalArgs[2].Trim();

        if (!ConsoleCommandHelper.TryFindEntity(_runtime, entityName, out var entity, out errorMessage))
            return false;

        if (!ConsoleCommandHelper.TrySetDataPreservingExistingType(entity!, key, raw, out errorMessage))
            return false;

        outputChannel.Publish($"[{entityName}] {key} = {raw}");
        errorMessage = null;
        return true;
    }
}
