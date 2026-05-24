using System;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     数据存取与订阅能力，从 <see cref="ISndEntity" /> 中拆分，遵循接口隔离原则。
/// </summary>
public interface ISndDataAccess
{
    void SetData<T>(string name, T value);

    T GetData<T>(string name);

    (bool found, T? value) TryGetData<T>(string name);

    /// <summary>
    ///     订阅指定键的数据变更通知。
    /// </summary>
    void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null);

    /// <summary>
    ///     取消订阅指定键的数据变更通知。
    /// </summary>
    void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback);
}
