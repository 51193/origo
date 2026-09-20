using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary><c>delete_save &lt;saveId&gt;</c> — delete an inactive save slot and its remnants.</summary>
internal sealed class DeleteSaveCommandHandler : ConsoleCommandHandlerBase
{
    private readonly ISndSaveOperations _saveOperations;

    public DeleteSaveCommandHandler(ISndSaveOperations saveOperations)
    {
        ArgumentNullException.ThrowIfNull(saveOperations);
        _saveOperations = saveOperations;
    }

    public override string Name => "delete_save";
    public override string HelpText => "delete_save <saveId> — delete an inactive save slot.";
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
            _saveOperations.DeleteSave(saveId);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        outputChannel.Publish($"Save '{saveId}' deleted.");
        errorMessage = null;
        return true;
    }
}
