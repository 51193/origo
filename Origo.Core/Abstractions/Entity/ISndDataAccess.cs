namespace Origo.Core.Abstractions.Entity;

/// <summary>Strongly-typed data access on an SND entity.</summary>
public interface ISndDataAccess
{
    /// <summary>
    /// Sets a typed data value by key. The value must not be null when
    /// <typeparamref name="T"/> is a reference type.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is null and <typeparamref name="T"/> is a reference type.
    /// </exception>
    void SetData<T>(string name, T value);

    /// <summary>Try to get a typed data value by key. Returns (false, default) if not found or type mismatch.</summary>
    (bool found, T? value) TryGetData<T>(string name);

    /// <summary>
    ///     Try to get a typed data value by key into <paramref name="value" />.
    ///     Returns false (with default) if not found or the type mismatches.
    /// </summary>
    bool TryGetData<T>(string name, out T? value);

    /// <summary>
    /// Gets a strongly-typed data value by key.
    /// Throws <see cref="InvalidOperationException"/> if the key is not found
    /// or the value is not of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Must be a non-nullable type.</typeparam>
    /// <exception cref="InvalidOperationException">The key is not found or the value is not of the expected type.</exception>
    T GetData<T>(string name) where T : notnull;
}
