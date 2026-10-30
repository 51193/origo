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
    /// <exception cref="System.ArgumentException">
    ///     Thrown when <paramref name="key" /> is null or whitespace.
    /// </exception>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown when <paramref name="value" /> is null and <typeparamref name="T" />
    ///     has no registered TypedData kind, because the type cannot be recovered
    ///     from a null reference.
    /// </exception>
    void SetValue<T>(string key, T value);

    /// <summary>
    ///     Gets a typed value; reports whether the key exists with a matching type.
    ///     The value is returned as <c>T?</c> because a stored null of a registered
    ///     reference kind (for example <c>string</c>) is legal: the entry exists, so
    ///     <c>found</c> is true while the returned value is null. Callers must handle
    ///     the nullable result instead of assuming a found value is non-null.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The key to read.</param>
    /// <exception cref="System.ArgumentException">
    ///     Thrown when <paramref name="key" /> is null or whitespace.
    /// </exception>
    (bool found, T? value) TryGet<T>(string key);

    /// <summary>Removes all entries from the blackboard.</summary>
    void Clear();

    /// <summary>Returns a snapshot of all currently stored keys; later mutations do not change it.</summary>
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
