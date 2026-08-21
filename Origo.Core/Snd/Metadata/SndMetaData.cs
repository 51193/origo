using System;
using System.Collections.Generic;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Framework-level metadata aggregation for SND entities.
///     Engine-agnostic; contains only name, node metadata, strategy lists, and data.
/// </summary>
public sealed class SndMetaData
{
    /// <summary>
    ///     Stable name identifier of the entity. Callers must keep names unique
    ///     within a session.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Node metadata (logical name → resource ID mapping), used by the engine adapter layer to create nodes.</summary>
    public NodeMetaData? NodeMetaData { get; set; }

    /// <summary>Strategy metadata containing a list of strategy indices.</summary>
    public StrategyMetaData? StrategyMetaData { get; set; }

    /// <summary>Data metadata containing entity key-value data (TypedData mapping).</summary>
    public DataMetaData? DataMetaData { get; set; } = new();

    /// <summary>
    ///     Deep-clones the metadata container (new dictionaries and lists;
    ///     <see cref="TypedData" /> instances and their <c>Data</c> references are handled similarly
    ///     to JSON round-trip deep copy, without recursively copying object graphs).
    /// </summary>
    public SndMetaData DeepClone()
    {
        return new SndMetaData
        {
            Name = Name,
            NodeMetaData = NodeMetaData is null
                ? null
                : new NodeMetaData
                {
                    Pairs = new Dictionary<string, string>(NodeMetaData.Pairs, StringComparer.Ordinal)
                },
            StrategyMetaData = StrategyMetaData is null
                ? null
                : new StrategyMetaData
                {
                    LifecycleIndices = [.. StrategyMetaData.LifecycleIndices],
                    ActiveIndices = [.. StrategyMetaData.ActiveIndices],
                    ObserverIndices = StrategyMetaData.ObserverIndices
                        .ConvertAll(b => new StrategyMetaData.ObserverBinding
                        {
                            Target = b.Target,
                            ObserverIndices = [.. b.ObserverIndices]
                        })
                },
            DataMetaData = DataMetaData is null
                ? null
                : new DataMetaData
                {
                    Pairs = new Dictionary<string, TypedData>(DataMetaData.Pairs)
                }
        };
    }
}
