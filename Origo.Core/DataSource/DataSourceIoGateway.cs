using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.Core.DataSource;

/// <summary>
///     DataSource 文件 I/O 中间层默认实现。
/// </summary>
internal sealed class DataSourceIoGateway : IDataSourceIoGateway
{
    private readonly Dictionary<DataSourceCodecKind, IDataSourceCodec> _codecs;
    private readonly IFileSystem _fileSystem;
    private readonly DataSourceIoOptions _options;

    public DataSourceIoGateway(
        IFileSystem fileSystem,
        DataSourceIoOptions options,
        bool writeIndented = true)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(options);
        _fileSystem = fileSystem;
        _options = options;
        _codecs = new Dictionary<DataSourceCodecKind, IDataSourceCodec>
        {
            [DataSourceCodecKind.Json] = new JsonDataSourceCodec(writeIndented),
            [DataSourceCodecKind.Map] = new MapDataSourceCodec()
        };
    }

    public bool Exists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("DataSource file path cannot be null or whitespace.", nameof(filePath));
        return _fileSystem.Exists(filePath);
    }

    public DataSourceNode ReadTree(string filePath)
    {
        var codec = ResolveCodec(filePath, out var suffix);
        var rawText = _fileSystem.ReadAllText(filePath);
        try
        {
            return codec.Decode(rawText);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to decode DataSource file '{filePath}' with suffix '{suffix}'.",
                ex);
        }
    }

    public void WriteTree(string filePath, DataSourceNode node, bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        var codec = ResolveCodec(filePath, out var suffix);
        string rawText;
        try
        {
            rawText = codec.Encode(node);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to encode DataSource tree for file '{filePath}' with suffix '{suffix}'.",
                ex);
        }

        _fileSystem.WriteAllText(filePath, rawText, overwrite);
    }

    private IDataSourceCodec ResolveCodec(string filePath, out string normalizedSuffix)
    {
        if (!_options.TryResolveCodecKind(filePath, out var codecKind, out normalizedSuffix))
            throw new InvalidOperationException(
                $"No DataSource codec configured for file '{filePath}' (suffix '{normalizedSuffix}').");

        if (!_codecs.TryGetValue(codecKind, out var codec))
            throw new InvalidOperationException(
                $"DataSource codec '{codecKind}' required by file '{filePath}' is not registered.");

        return codec;
    }
}
