using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Logging;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd;

/// <summary>
///     Manages SND-related runtime mappings:
///     scene resource aliases → engine-specific resource paths,
///     and template aliases → SndMetaData templates.
///     Attached as an instance on <see cref="SndWorld" /> and managed alongside the runtime lifecycle.
/// </summary>
internal sealed class SndMappings
{
    /// <summary>Detects Godot-style schemes (<c>res://</c>, <c>user://</c>) and other URI-like resource ids.</summary>
    private const string _uriLikeSchemeSeparator = "://";

    /// <summary>JSON key for template reference in meta list shorthand entries.</summary>
    private const string _templateKeyField = "templateKey";

    /// <summary>JSON key for entity display name in meta list shorthand entries.</summary>
    private const string _sndNameField = "sndName";

    private readonly Dictionary<string, string> _sceneAliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _templatePaths = new(StringComparer.Ordinal);
    private SndTemplateResolver? _templateResolver;

    /// <summary>
    ///     Loads scene resource alias mappings from the specified text file.
    ///     The file format is line-based <c>key: value</c> pairs;
    ///     blank lines and lines starting with # are ignored.
    ///     The previous mappings are replaced only after the load succeeds;
    ///     a failed reload does not destroy the existing state.
    /// </summary>
    public void LoadSceneAliases(IDataSourceIoGateway dataSourceIo, string mapFilePath, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(logger);
        if (string.IsNullOrWhiteSpace(mapFilePath))
            throw new ArgumentException("Scene alias map file path cannot be null or whitespace.", nameof(mapFilePath));

        using var node = dataSourceIo.ReadTree(mapFilePath);
        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in node.Keys)
            loaded[key] = node[key].AsString();

        _sceneAliases.Clear();
        foreach (var (key, value) in loaded)
            _sceneAliases[key] = value;
        logger.Log(LogLevel.Info, nameof(SndMappings),
            new LogMessageBuilder().AddContext("filePath", mapFilePath)
                .Build($"Loaded {_sceneAliases.Count} scene resource aliases."));
    }

    /// <summary>
    ///     Resolves a node resource identifier into a concrete resource path.
    ///     Strict mode: if it is not an explicit resource path (e.g., res://, user://)
    ///     and the alias does not exist, an exception is thrown.
    /// </summary>
    public string ResolveSceneAlias(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Scene id cannot be null or whitespace.", nameof(id));

        if (IsExplicitResourcePath(id))
            return id;

        if (_sceneAliases.TryGetValue(id, out var mapped))
            return mapped;

        throw new KeyNotFoundException($"Scene alias '{id}' not found in scene alias map.");
    }

    /// <summary>
    ///     Loads the mapping from template aliases to JSON file paths from the map file,
    ///     and configures the internal codec.
    ///     The previous mapping and resolver are replaced only after the load
    ///     succeeds; a failed reload does not destroy the existing state.
    /// </summary>
    public void LoadTemplates(
        IDataSourceIoGateway dataSourceIo,
        string mapFilePath,
        DataSourceConverterRegistry registry,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);
        if (string.IsNullOrWhiteSpace(mapFilePath))
            throw new ArgumentException("Snd template alias map file path cannot be null or whitespace.",
                nameof(mapFilePath));

        using var node = dataSourceIo.ReadTree(mapFilePath);
        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in node.Keys)
            loaded[key] = node[key].AsString();

        var sndMetaConverter = registry.Get<SndMetaData>();
        var resolver = new SndTemplateResolver(dataSourceIo, sndMetaConverter, loaded);

        _templatePaths.Clear();
        foreach (var (key, value) in loaded)
            _templatePaths[key] = value;
        _templateResolver = resolver;
        logger.Log(LogLevel.Info, nameof(SndMappings),
            new LogMessageBuilder().AddContext("filePath", mapFilePath)
                .Build($"Loaded {_templatePaths.Count} Snd templates."));
    }

    /// <summary>
    ///     Resolves and loads an SndMetaData template by alias
    ///     (strict mode: throws immediately on missing/uninitialized/resolution failure).
    /// </summary>
    public SndMetaData ResolveTemplate(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Template alias cannot be null or whitespace.", nameof(alias));

        if (_templateResolver is null)
            throw new InvalidOperationException(
                "Template resolution called before LoadTemplates; template paths are not initialized.");

        if (_templatePaths.Count == 0)
            throw new InvalidOperationException(
                "No templates loaded: the template map is empty. Call LoadTemplates with a map that contains at least one template entry before resolving template references.");

        return _templateResolver.Resolve(alias);
    }

    /// <summary>
    ///     Resolves a DataSourceNode array (which may contain template reference shorthand)
    ///     into an SndMetaData list. Supports two forms: full SndMetaData objects,
    ///     or { "sndName": "...", "templateKey": "..." } shorthand.
    /// </summary>
    public IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(
        DataSourceNode root,
        DataSourceConverterRegistry registry)
    {
        var list = new List<SndMetaData>();
        var sndMetaConverter = registry.Get<SndMetaData>();

        foreach (var item in root.Elements)
            if (item.Kind == DataSourceNodeKind.Map && item.ContainsKey(_templateKeyField))
            {
                var templateKey = item[_templateKeyField].AsString();
                if (string.IsNullOrWhiteSpace(templateKey))
                    throw new InvalidOperationException($"Config entry has an empty '{_templateKeyField}'.");

                var sndName = item.TryGetValue(_sndNameField, out var sndNameNode) && sndNameNode is not null
                    ? sndNameNode.AsString()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(sndName))
                    throw new InvalidOperationException(
                        $"Config entry referencing template '{templateKey}' has an empty '{_sndNameField}'.");

                var template = ResolveTemplate(templateKey);

                var cloned = template.DeepClone();
                cloned.Name = sndName;
                list.Add(cloned);
            }
            else
            {
                var meta = sndMetaConverter.Read(item) ?? throw new InvalidOperationException("Failed to deserialize SndMetaData from config entry.");
                if (string.IsNullOrWhiteSpace(meta.Name))
                    throw new InvalidOperationException("SndMetaData 'name' cannot be empty.");
                list.Add(meta);
            }

        return list;
    }

    private static bool IsExplicitResourcePath(string id) =>
        id.Contains(_uriLikeSchemeSeparator, StringComparison.Ordinal);
}
