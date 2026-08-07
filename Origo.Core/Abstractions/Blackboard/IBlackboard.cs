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
    /// <summary>Sets a typed value under a key.</summary>
    /// <typeparam name="T">The value type; preserved across serialization.</typeparam>
    /// <param name="key">The key to store under.</param>
    /// <param name="value">The value to store.</param>
    void SetValue<T>(string key, T value);

    /// <summary>Gets a typed value; reports whether the key exists with a matching type.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The key to read.</param>
    (bool found, T value) TryGet<T>(string key);

    /// <summary>Removes all entries from the blackboard.</summary>
    void Clear();

    /// <summary>Enumerates all currently stored keys.</summary>
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
