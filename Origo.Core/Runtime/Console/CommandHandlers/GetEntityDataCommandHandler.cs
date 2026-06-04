using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>entity_get_data</c> 命令：按名称和键读取 SND 实体数据。
///     用法：<c>entity_get_data &lt;entity&gt; &lt;key&gt;</c>
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
    public override string HelpText => "entity_get_data <entity> <key> — 读取实体数据的值及类型。";
    public override int MinPositionalArgs => 2;
    public override int MaxPositionalArgs => 2;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();
        var key = invocation.PositionalArgs[1].Trim();

        var entity = _runtime.Snd.FindByName(entityName);
        if (entity is null)
        {
            errorMessage = $"Entity '{entityName}' not found.";
            return false;
        }

        var (found, value) = entity.TryGetData<object>(key);
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
