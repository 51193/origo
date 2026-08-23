namespace Origo.Core.Save.Storage;

/// <summary>
///     Default implementation of <see cref="ISavePathPolicy" />, delegating
///     to <see cref="SavePathLayout" /> static methods.
/// </summary>
internal sealed class DefaultSavePathPolicy : ISavePathPolicy
{
    /// <inheritdoc/>
    public string GetCurrentDirectory() => SavePathLayout.GetCurrentDirectory();

    /// <inheritdoc/>
    public string GetSaveDirectory(string saveId) => SavePathLayout.GetSaveDirectory(saveId);

    /// <inheritdoc/>
    public string GetProgressFile(string baseDirectory) => SavePathLayout.GetProgressFile(baseDirectory);

    /// <inheritdoc/>
    public string GetProgressStateMachinesFile(string baseDirectory) =>
        SavePathLayout.GetProgressStateMachinesFile(baseDirectory);

    /// <inheritdoc/>
    public string GetCustomMetaFile(string baseDirectory) => SavePathLayout.GetCustomMetaFile(baseDirectory);

    /// <inheritdoc/>
    public string GetLevelDirectory(string baseDirectory, string levelId) =>
        SavePathLayout.GetLevelDirectory(baseDirectory, levelId);

    /// <inheritdoc/>
    public string GetLevelSndSceneFile(string levelDirectory) => SavePathLayout.GetLevelSndSceneFile(levelDirectory);

    /// <inheritdoc/>
    public string GetLevelSessionFile(string levelDirectory) => SavePathLayout.GetLevelSessionFile(levelDirectory);

    /// <inheritdoc/>
    public string GetLevelSessionStateMachinesFile(string levelDirectory) =>
        SavePathLayout.GetLevelSessionStateMachinesFile(levelDirectory);

    /// <inheritdoc/>
    public string GetWriteInProgressMarker(string baseDirectory) =>
        SavePathLayout.GetWriteInProgressMarker(baseDirectory);

    /// <inheritdoc/>
    public string GetPayloadShaFile(string baseDirectory) => SavePathLayout.GetPayloadShaFile(baseDirectory);

    /// <inheritdoc/>
    public string GetExtraDirectory(string baseDirectory) => SavePathLayout.GetExtraDirectory(baseDirectory);
}
