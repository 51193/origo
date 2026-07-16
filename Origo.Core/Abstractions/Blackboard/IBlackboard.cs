using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Blackboard;

/// <summary>
///     General-purpose key-value blackboard interface for Core-layer
///     global/progress/session-level shared state. Uses
///     <see cref="TypedData" /> internally to preserve type information,
///     ensuring types survive serialization/deserialization.
/// </summary>
public interface IBlackboard
{
    void SetValue<T>(string key, T value);

    (bool found, T value) TryGet<T>(string key);

    void Clear();

    IReadOnlyCollection<string> GetKeys();

    /// <summary>
    ///     Export all entries with type information, for serialization
    ///     and persistence.
    /// </summary>
    IReadOnlyDictionary<string, TypedData> SerializeAll();

    /// <summary>
    ///     Restore all entries from a type-annotated dictionary,
    ///     replacing current contents.
    /// </summary>
    void DeserializeAll(IReadOnlyDictionary<string, TypedData> data);
}
