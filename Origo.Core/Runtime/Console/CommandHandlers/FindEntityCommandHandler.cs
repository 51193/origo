using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>find_entity</c> 命令：按名称查找 SND 实体并显示其数据键。
///     用法：<c>find_entity &lt;name&gt;</c>
/// </summary>
internal sealed class FindEntityCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;

    public FindEntityCommandHandler(OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public override string Name => "find_entity";
    public override string HelpText => "find_entity <name> — 按名称查找 SND 实体并显示其节点信息。";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var name = invocation.PositionalArgs[0].Trim();
        var entity = _runtime.Snd.FindByName(name);
        if (entity is null)
        {
            outputChannel.Publish($"Entity '{name}' not found.");
        }
        else
        {
            var nodeNames = entity.GetNodeNames();
            outputChannel.Publish($"Entity '{name}' found. Nodes: [{string.Join(", ", nodeNames)}]");
        }

        errorMessage = null;
        return true;
    }
}
