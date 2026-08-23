using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Save.Meta;

/// <summary>
///     Read-only <see cref="IBlackboard" /> adapter used by
///     <see cref="SaveMetaBuildContext" />. Read operations delegate to the
///     wrapped blackboard; every mutation operation throws
///     <see cref="InvalidOperationException" /> so save-meta contributors
///     cannot change progress/session state through their read-only context.
/// </summary>
internal sealed class ReadOnlyBlackboard(IBlackboard inner) : IBlackboard
{
    private readonly IBlackboard _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc/>
    public void SetValue<T>(string key, T value) => ThrowReadOnly();

    /// <inheritdoc/>
    public (bool found, T value) TryGet<T>(string key) => _inner.TryGet<T>(key);

    /// <inheritdoc/>
    public void Clear() => ThrowReadOnly();

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetKeys() => _inner.GetKeys();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, TypedData> SerializeAll() => _inner.SerializeAll();

    /// <inheritdoc/>
    public void DeserializeAll(IReadOnlyDictionary<string, TypedData> data) => ThrowReadOnly();

    private static void ThrowReadOnly() =>
        throw new InvalidOperationException(
            "SaveMetaBuildContext blackboards are read-only. " +
            "Contributors must return their metadata dictionary instead of mutating progress/session state.");
}
