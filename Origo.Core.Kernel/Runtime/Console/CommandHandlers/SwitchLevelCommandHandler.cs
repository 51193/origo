using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary><c>switch_level &lt;levelId&gt;</c> — queue a foreground-level switch request.</summary>
internal sealed class SwitchLevelCommandHandler : ConsoleCommandHandlerBase
{
    private readonly ISndSaveOperations _saveOperations;

    public SwitchLevelCommandHandler(ISndSaveOperations saveOperations)
    {
        ArgumentNullException.ThrowIfNull(saveOperations);
        _saveOperations = saveOperations;
    }

    public override string Name => "switch_level";
    public override string HelpText => "switch_level <levelId> — queue a foreground-level switch.";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var levelId = invocation.PositionalArgs[0].Trim();
        try
        {
            _saveOperations.RequestSwitchForegroundLevel(levelId);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        outputChannel.Publish($"Level switch queued for '{levelId}'.");
        errorMessage = null;
        return true;
    }
}
