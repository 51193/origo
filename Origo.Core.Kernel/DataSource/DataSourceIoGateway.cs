using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.FileSystem;

namespace Origo.Core.DataSource;

/// <summary>
///     Default implementation of the DataSource file I/O intermediate layer. All file content read/write
///     operations are routed to the corresponding <see cref="IDataSourceCodec" /> via suffix, and
///     encode/decode exceptions are uniformly wrapped as <see cref="InvalidOperationException" />
///     (including file path and suffix information).
/// </summary>
internal sealed class DataSourceIoGateway : IDataSourceIoGateway
{
    private readonly Dictionary<DataSourceCodecKind, IDataSourceCodec> _codecs;
    private readonly IFileSystem _fileSystem;
    private readonly DataSourceIoOptions _options;

    public DataSourceIoGateway(
        IFileSystem fileSystem,
        DataSourceIoOptions options,
        IReadOnlyDictionary<DataSourceCodecKind, IDataSourceCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(codecs);
        _fileSystem = fileSystem;
        _options = options;
        _codecs = new Dictionary<DataSourceCodecKind, IDataSourceCodec>(codecs);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
