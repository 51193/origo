namespace Origo.Core.Abstractions.Entity;

/// <summary>Strongly-typed data access on an SND entity.</summary>
public interface ISndDataAccess
{
    /// <summary>
    ///     Sets a typed data value by key. Entity data values are always non-null;
    ///     storing <c>null</c> is rejected instead of leaving an unreachable entry.
    /// </summary>
    /// <exception cref="System.ArgumentException">
    ///     Thrown when <paramref name="name" /> is null or whitespace.
    /// </exception>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown when <paramref name="value" /> is null.
    /// </exception>
    void SetData<T>(string name, T value);

    /// <summary>Try to get a typed data value by key. Returns (false, default) if not found or type mismatch.</summary>
    /// <exception cref="System.ArgumentException">
    ///     Thrown when <paramref name="name" /> is null or whitespace.
    /// </exception>
    (bool found, T? value) TryGetData<T>(string name);

    /// <summary>
    ///     Try to get a typed data value by key into <paramref name="value" />.
    ///     Returns false (with default) if not found or the type mismatches.
    /// </summary>
    /// <exception cref="System.ArgumentException">
    ///     Thrown when <paramref name="name" /> is null or whitespace.
    /// </exception>
    bool TryGetData<T>(string name, out T? value);

    /// <summary>
    /// Gets a strongly-typed data value by key.
    /// Throws <see cref="System.InvalidOperationException"/> if the key is not found
    /// or the value is not of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Must be a non-nullable type.</typeparam>
    /// <exception cref="System.ArgumentException">
    ///     Thrown when <paramref name="name" /> is null or whitespace.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">The key is not found or the value is not of the expected type.</exception>
    T GetData<T>(string name) where T : notnull;
}
