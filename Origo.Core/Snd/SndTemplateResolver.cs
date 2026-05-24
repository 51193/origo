using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd;

/// <summary>
///     在已加载的模板路径映射上解析 <see cref="SndMetaData" />，带内存缓存。
/// </summary>
internal sealed class SndTemplateResolver
{
    private readonly Dictionary<string, SndMetaData> _cache = new(StringComparer.Ordinal);
    private readonly DataSourceConverter<SndMetaData> _converter;
    private readonly IDataSourceIoGateway _dataSourceIo;
    private readonly Dictionary<string, string> _paths;

    public SndTemplateResolver(
        IDataSourceIoGateway dataSourceIo,
        DataSourceConverter<SndMetaData> converter,
        Dictionary<string, string> templatePaths)
    {
        ArgumentNullException.ThrowIfNull(dataSourceIo);
        ArgumentNullException.ThrowIfNull(converter);
        _dataSourceIo = dataSourceIo;
        _converter = converter;
        _paths = new Dictionary<string, string>(templatePaths, StringComparer.Ordinal);
    }

    public SndMetaData Resolve(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Template alias cannot be null or whitespace.", nameof(alias));

        if (_cache.TryGetValue(alias, out var cached))
            return cached;

        if (!_paths.TryGetValue(alias, out var path))
            throw new KeyNotFoundException($"Template alias '{alias}' not found in template map.");

        using var node = _dataSourceIo.ReadTree(path);
        var meta = _converter.Read(node);
        if (meta is null)
            throw new InvalidOperationException($"Template '{alias}' at '{path}' deserialized to null.");

        _cache[alias] = meta;
        return meta;
    }
}
