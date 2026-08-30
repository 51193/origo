using System;
using System.Collections.Generic;
using Origo.Core.Save.Meta;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Persistence-related operations: save/load and level switching.
/// </summary>
public interface ISndSaveOperations
{
    /// <summary>List available save slots.</summary>
    IReadOnlyList<string> ListSaves();

    /// <summary>
    ///     Lists available save slots with their display metadata read from
    ///     each snapshot's <c>meta.map</c>. Framework-reserved <c>origo.*</c>
    ///     keys are stripped from the returned metadata.
    /// </summary>
    IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData();

    /// <summary>Request to load a specific save.</summary>
    void RequestLoadGame(string saveId);

    /// <summary>Request to save to a specific slot.</summary>
    void RequestSaveGame(string newSaveId);

    /// <summary>Auto-save, returning the actual saveId used.</summary>
    string RequestSaveGameAuto(string? newSaveId = null);

    /// <summary>Set the continue target save.</summary>
    void SetContinueTarget(string saveId);

    /// <summary>Request to switch the foreground level.</summary>
    void RequestSwitchForegroundLevel(string newLevelId);

    /// <summary>
    ///     Register a display <c>meta.map</c> contributor, executed on every
    ///     <see cref="RequestSaveGame" />.
    /// </summary>
    void RegisterSaveMetaContributor(ISaveMetaContributor contributor);

    /// <summary>
    ///     Register a display <c>meta.map</c> contributor via delegate.
    /// </summary>
    void RegisterSaveMetaContributor(Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> contribute);
}
