using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>help</c> 命令：列出所有已注册的控制台命令及其帮助信息。
/// </summary>
internal sealed class HelpCommandHandler : ConsoleCommandHandlerBase
{
    private readonly ConsoleCommandRouter _router;

    public HelpCommandHandler(ConsoleCommandRouter router)
    {
        ArgumentNullException.ThrowIfNull(router);
        _router = router;
    }

    public override string Name => "help";
    public override string HelpText => "help — 列出所有可用命令及其帮助信息。";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var handlers = _router.GetRegisteredHandlers();
        outputChannel.Publish($"Available commands ({handlers.Count}):");
        foreach (var handler in handlers)
            outputChannel.Publish($"  {handler.HelpText}");

        errorMessage = null;
        return true;
    }
}
