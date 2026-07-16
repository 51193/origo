namespace Origo.Core.Save.Storage;

/// <summary>
///     Default implementation of <see cref="ISavePathPolicy" />, delegating
///     to <see cref="SavePathLayout" /> static methods.
/// </summary>
internal sealed class DefaultSavePathPolicy : ISavePathPolicy
{
    public string GetCurrentDirectory() => SavePathLayout.GetCurrentDirectory();

    public string GetSaveDirectory(string saveId) => SavePathLayout.GetSaveDirectory(saveId);

    public string GetProgressFile(string baseDirectory) => SavePathLayout.GetProgressFile(baseDirectory);

    public string GetProgressStateMachinesFile(string baseDirectory) =>
        SavePathLayout.GetProgressStateMachinesFile(baseDirectory);

    public string GetCustomMetaFile(string baseDirectory) => SavePathLayout.GetCustomMetaFile(baseDirectory);

    public string GetLevelDirectory(string baseDirectory, string levelId) =>
        SavePathLayout.GetLevelDirectory(baseDirectory, levelId);

    public string GetLevelSndSceneFile(string levelDirectory) => SavePathLayout.GetLevelSndSceneFile(levelDirectory);

    public string GetLevelSessionFile(string levelDirectory) => SavePathLayout.GetLevelSessionFile(levelDirectory);

    public string GetLevelSessionStateMachinesFile(string levelDirectory) =>
        SavePathLayout.GetLevelSessionStateMachinesFile(levelDirectory);

    public string GetWriteInProgressMarker(string baseDirectory) =>
        SavePathLayout.GetWriteInProgressMarker(baseDirectory);

    public string GetPayloadShaFile(string baseDirectory) => SavePathLayout.GetPayloadShaFile(baseDirectory);

    public string GetExtraDirectory(string baseDirectory) => SavePathLayout.GetExtraDirectory(baseDirectory);
}
