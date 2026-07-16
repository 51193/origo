using System;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary>
///     <c>help</c> command: lists all registered console commands and their help text.
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
    public override string HelpText => "help — list all available commands and their help text.";
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
