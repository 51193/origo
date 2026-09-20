using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary><c>save &lt;saveId&gt;</c> — queue a save request for the named slot.</summary>
internal sealed class SaveGameCommandHandler : ConsoleCommandHandlerBase
{
    private readonly ISndSaveOperations _saveOperations;

    public SaveGameCommandHandler(ISndSaveOperations saveOperations)
    {
        ArgumentNullException.ThrowIfNull(saveOperations);
        _saveOperations = saveOperations;
    }

    public override string Name => "save";
    public override string HelpText => "save <saveId> — queue a save request for a slot.";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var saveId = invocation.PositionalArgs[0].Trim();
        try
        {
            _saveOperations.RequestSaveGame(saveId);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        outputChannel.Publish($"Save request queued for '{saveId}'.");
        errorMessage = null;
        return true;
    }
}
