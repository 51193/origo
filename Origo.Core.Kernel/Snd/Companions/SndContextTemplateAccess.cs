using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Snd;
using Origo.Core.DataSource;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Companions;

/// <summary>Template loading, cloning, and entity-list resolution for <see cref="SndContext" />.</summary>
internal sealed class SndContextTemplateAccess(SndContext owner) : ISndTemplateAccess
{
    /// <inheritdoc/>
    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
    {
        var cloned = owner.Runtime.SndWorld.ResolveTemplate(templateKey);
        if (!string.IsNullOrWhiteSpace(overrideName))
            cloned.Name = overrideName;
        return cloned;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(DataSourceNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return owner.Runtime.SndWorld.ResolveMetaListFromJsonArray(root);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SndMetaData> LoadMetaListFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var root = owner.DataSourceIo.ReadTree(filePath);
        return owner.Runtime.SndWorld.ResolveMetaListFromJsonArray(root);
    }

    /// <inheritdoc/>
    public void LoadTemplates(string mapFilePath) =>
        owner.Runtime.SndWorld.LoadTemplates(mapFilePath, owner.Runtime.Logger);

    /// <inheritdoc/>
    public void LoadSceneAliases(string mapFilePath) =>
        owner.Runtime.SndWorld.LoadSceneAliases(mapFilePath, owner.Runtime.Logger);
}
