using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     Template and entity-mapping capability. Templates can be loaded from a
///     map file, cloned by key (deep copy), and resolved from JSON arrays —
///     including <c>{ "sndName": "...", "templateKey": "..." }</c> shorthand.
///     Scene alias maps used by template node metadata can also be loaded at
///     runtime through this companion.
/// </summary>
public interface ISndTemplateAccess
{
    /// <summary>Clone a template and optionally override the name, for batch entity creation.</summary>
    SndMetaData CloneTemplate(string templateKey, string? overrideName = null);

    /// <summary>
    ///     Resolves a JSON array into entity metadata, expanding template
    ///     shorthand entries and fully deserializing regular SndMetaData
    ///     objects.
    /// </summary>
    IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(DataSourceNode root);

    /// <summary>
    ///     Reads a JSON array file and resolves it into entity metadata,
    ///     expanding template shorthand entries. Pair with
    ///     <c>ISessionRun.SpawnMany</c> to spawn the returned entities.
    /// </summary>
    IReadOnlyList<SndMetaData> LoadMetaListFromFile(string filePath);

    /// <summary>Loads or reloads the SND template map from the given map file.</summary>
    void LoadTemplates(string mapFilePath);

    /// <summary>Loads or reloads the scene alias map from the given map file.</summary>
    void LoadSceneAliases(string mapFilePath);
}
