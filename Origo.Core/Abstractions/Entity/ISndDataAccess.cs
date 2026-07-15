namespace Origo.Core.Abstractions.Entity;

/// <summary>Strongly-typed data access on an SND entity.</summary>
public interface ISndDataAccess
{
    /// <summary>Set a typed data value by key.</summary>
    void SetData<T>(string name, T value);

    /// <summary>Try to get a typed data value by key. Returns (false, default) if not found or type mismatch.</summary>
    (bool found, T? value) TryGetData<T>(string name);

    /// <summary>Get a typed data value by key. Returns default if not found or type mismatch.</summary>
    T GetData<T>(string name);
}
