using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>entity_get_data</c> command: reads SND entity data by name and key.
///     Usage: <c>entity_get_data &lt;entity&gt; &lt;key&gt;</c>
/// </summary>
internal sealed class GetEntityDataCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public GetEntityDataCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "entity_get_data";
    public override string HelpText => "entity_get_data <entity> <key> — read an entity data value and its type.";
    public override int MinPositionalArgs => 2;
    public override int MaxPositionalArgs => 2;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();
        var key = invocation.PositionalArgs[1].Trim();

        if (!ConsoleCommandHelper.TryFindEntity(_runtime, entityName, out var entity, out errorMessage))
            return false;

        var (found, value) = entity!.TryGetData<object>(key);
        if (found)
        {
            var typeName = value?.GetType().Name ?? "null";
            outputChannel.Publish($"{key} = {value} (type: {typeName})");
        }
        else
        {
            outputChannel.Publish($"Key '{key}' not found on entity '{entityName}'.");
        }

        errorMessage = null;
        return true;
    }
}
