using System;
using System.Collections.Generic;

namespace Origo.Core.DataSource.Converters;

/// <summary>
///     Shared <c>pairs</c> envelope for the metadata converters. Data and node
///     metadata differ only in the element type they carry, so the strict read
///     validation and the envelope shape live here instead of being repeated in
///     each converter: a metadata value must be a JSON object, and its optional
///     <c>pairs</c> member must itself be a JSON object.
/// </summary>
internal static class MetaDataPairsCodec
{
    private const string _pairsKey = "pairs";

    /// <summary>Validates that the node holding metadata is a JSON object.</summary>
    /// <param name="node">Node to validate.</param>
    /// <param name="label">Converter name used in the failure message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the node is not a map.</exception>
    public static void ValidateMetaNode(DataSourceNode node, string label)
    {
        if (node.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"{label} must be a JSON object, but found {node.Kind}. " +
                "The save data is corrupt and cannot be recovered.");
    }

    /// <summary>Reads the optional <c>pairs</c> map into an existing dictionary.</summary>
    /// <typeparam name="TValue">Element type the converter produces.</typeparam>
    /// <param name="node">Metadata object that may carry a <c>pairs</c> member.</param>
    /// <param name="label">Converter name used in the failure message.</param>
    /// <param name="pairs">Dictionary filled with the converted pairs.</param>
    /// <param name="readValue">Converter for one pair value.</param>
    /// <exception cref="InvalidOperationException">Thrown when <c>pairs</c> is present but not a map.</exception>
    /// <remarks>
    ///     The dictionary is taken concretely, not through
    ///     <see cref="IDictionary{TKey,TValue}" />: the metadata contracts expose
    ///     <see cref="Dictionary{TKey,TValue}" />, and iterating the concrete type
    ///     keeps the struct enumerator and the direct indexer, exactly like the
    ///     per-converter loops this helper replaced.
    /// </remarks>
    public static void ReadPairs<TValue>(
        DataSourceNode node,
        string label,
        Dictionary<string, TValue> pairs,
        Func<DataSourceNode, TValue> readValue)
    {
        if (!node.TryGetValue(_pairsKey, out var pairsNode) || pairsNode is null || pairsNode.IsNull)
            return;

        if (pairsNode.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"{label} '{_pairsKey}' must be a JSON object, but found {pairsNode.Kind}. " +
                "The save data is corrupt and cannot be recovered.");

        foreach (var key in pairsNode.Keys)
            pairs[key] = readValue(pairsNode[key]);
    }

    /// <summary>Writes a metadata object carrying the given pairs.</summary>
    /// <typeparam name="TValue">Element type the converter consumes.</typeparam>
    /// <param name="pairs">Pairs to convert.</param>
    /// <param name="writeValue">Converter for one pair value.</param>
    /// <returns>A map node with a single <c>pairs</c> member.</returns>
    /// <remarks>
    ///     As in <see cref="ReadPairs{TValue}" />, the concrete dictionary type is
    ///     taken so the write path does not box the enumerator per serialization.
    /// </remarks>
    public static DataSourceNode WritePairs<TValue>(
        Dictionary<string, TValue> pairs,
        Func<TValue, DataSourceNode> writeValue)
    {
        var pairsNode = DataSourceNode.CreateObject();
        foreach (var pair in pairs)
            pairsNode.Add(pair.Key, writeValue(pair.Value));

        return DataSourceNode.CreateObject()
            .Add(_pairsKey, pairsNode);
    }
}
