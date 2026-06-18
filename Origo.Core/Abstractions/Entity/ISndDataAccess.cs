namespace Origo.Core.Abstractions.Entity;

public interface ISndDataAccess
{
    void SetData<T>(string name, T value);

    (bool found, T? value) TryGetData<T>(string name);
}
