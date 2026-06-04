using System;
using System.Globalization;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>entity_set_data</c> 命令：按名称、键和值设置 SND 实体数据，自动推断类型并保留已有键的类型。
///     用法：<c>entity_set_data &lt;entity&gt; &lt;key&gt; &lt;value&gt;</c>
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
    public override string HelpText => "entity_set_data <entity> <key> <value> — 设置实体数据（自动推断类型，保留已有键的类型）。";
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

        var entity = _runtime.Snd.FindByName(entityName);
        if (entity is null)
        {
            errorMessage = $"Entity '{entityName}' not found.";
            return false;
        }

        var (existing, existingObj) = entity.TryGetData<object>(key);
        if (existing && existingObj != null)
            SetByType(entity, key, raw, existingObj.GetType());
        else if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
            entity.SetData(key, iv);
        else if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
            entity.SetData(key, fv);
        else if (bool.TryParse(raw, out var bv))
            entity.SetData(key, bv);
        else
            entity.SetData(key, raw);

        outputChannel.Publish($"[{entityName}] {key} = {raw}");
        errorMessage = null;
        return true;
    }

    private static void SetByType(ISndEntity entity, string key, string raw, Type targetType)
    {
        if (targetType == typeof(int))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                entity.SetData(key, iv);
            return;
        }

        if (targetType == typeof(float))
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fv))
                entity.SetData(key, fv);
            return;
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(raw, out var bv))
                entity.SetData(key, bv);
            return;
        }

        if (targetType == typeof(string))
        {
            entity.SetData(key, raw);
            return;
        }

        entity.SetData(key, raw);
    }
}
