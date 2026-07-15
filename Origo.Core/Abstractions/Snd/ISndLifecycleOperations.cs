namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Save-related lifecycle entry points: continue game, initial save,
///     main menu entry.
/// </summary>
public interface ISndLifecycleOperations
{
    /// <summary>Whether a continue-target save exists.</summary>
    bool HasContinueData();

    /// <summary>Request to continue the game (based on the current continue target).</summary>
    bool RequestContinueGame();

    /// <summary>Request to load the initial save template.</summary>
    void RequestLoadInitialSave();

    /// <summary>Re-read the main menu entry configuration via the boot sequence.</summary>
    void RequestLoadMainMenuEntrySave();
}
