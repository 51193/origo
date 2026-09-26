using System;
using System.Linq;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary><c>load &lt;saveId&gt;</c> — queue a load request for an existing save slot.</summary>
internal sealed class LoadGameCommandHandler : ConsoleCommandHandlerBase
{
    private readonly ISndSaveOperations _saveOperations;

    public LoadGameCommandHandler(ISndSaveOperations saveOperations)
    {
        ArgumentNullException.ThrowIfNull(saveOperations);
        _saveOperations = saveOperations;
    }

    public override string Name => "load";
    public override string HelpText => "load <saveId> — queue a load request for an existing save slot.";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var saveId = invocation.PositionalArgs[0].Trim();
        if (!_saveOperations.ListSaves().Contains(saveId, StringComparer.Ordinal))
        {
            errorMessage = $"Save '{saveId}' does not exist.";
            return false;
        }

        try
        {
            _saveOperations.RequestLoadGame(saveId);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        outputChannel.Publish($"Load request queued for '{saveId}'.");
        errorMessage = null;
        return true;
    }
}
