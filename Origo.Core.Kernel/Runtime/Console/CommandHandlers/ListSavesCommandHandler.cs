using System;
using System.Linq;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Save.Meta;

namespace Origo.Core.Runtime.Console.CommandHandlers;

/// <summary><c>list_saves</c> — list save slots and their display metadata.</summary>
internal sealed class ListSavesCommandHandler : ConsoleCommandHandlerBase
{
    private readonly ISndSaveOperations _saveOperations;

    public ListSavesCommandHandler(ISndSaveOperations saveOperations)
    {
        ArgumentNullException.ThrowIfNull(saveOperations);
        _saveOperations = saveOperations;
    }

    public override string Name => "list_saves";
    public override string HelpText => "list_saves — list save slots and their display metadata.";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var saves = _saveOperations.ListSavesWithMetaData();
        if (saves.Count == 0)
        {
            outputChannel.Publish("No saves found.");
        }
        else
        {
            foreach (var save in saves)
                outputChannel.Publish(FormatSave(save));
        }

        errorMessage = null;
        return true;
    }

    private static string FormatSave(SaveMetaDataEntry save)
    {
        if (save.MetaData.Count == 0)
            return save.SaveId;

        var metadata = string.Join(", ",
            save.MetaData
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));
        return $"{save.SaveId} {metadata}";
    }
}
