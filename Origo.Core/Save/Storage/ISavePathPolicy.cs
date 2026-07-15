namespace Origo.Core.Save.Storage;

/// <summary>
///     Save path policy interface. Provides path assembly rules for save
///     directories and files, decoupling the path layout from business logic
///     so different path strategies can be injected per environment.
/// </summary>
public interface ISavePathPolicy
{
    /// <summary>Get the relative path of the active save directory (current/).</summary>
    string GetCurrentDirectory();

    /// <summary>Get the relative path of a snapshot directory by save ID.</summary>
    string GetSaveDirectory(string saveId);

    /// <summary>Get the relative path of the progress blackboard JSON file.</summary>
    string GetProgressFile(string baseDirectory);

    /// <summary>Get the relative path of the progress state machine snapshot JSON file.</summary>
    string GetProgressStateMachinesFile(string baseDirectory);

    /// <summary>Get the relative path of the custom metadata file.</summary>
    string GetCustomMetaFile(string baseDirectory);

    /// <summary>Get the relative path of a level save subdirectory.</summary>
    string GetLevelDirectory(string baseDirectory, string levelId);

    /// <summary>Get the relative path of the level SND scene JSON file.</summary>
    string GetLevelSndSceneFile(string levelDirectory);

    /// <summary>Get the relative path of the level session blackboard JSON file.</summary>
    string GetLevelSessionFile(string levelDirectory);

    /// <summary>Get the relative path of the level session state machine snapshot JSON file.</summary>
    string GetLevelSessionStateMachinesFile(string levelDirectory);

    /// <summary>Get the relative path of the write-in-progress marker file.</summary>
    string GetWriteInProgressMarker(string baseDirectory);

    /// <summary>Get the relative path of the payload SHA digest file.</summary>
    string GetPayloadShaFile(string baseDirectory);

    /// <summary>Get the relative path of the extra data subdirectory (e.g. current/extra).</summary>
    string GetExtraDirectory(string baseDirectory);
}
