using System;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     数据存取与订阅能力，从 <see cref="ISndEntity" /> 中拆分，遵循接口隔离原则。
/// </summary>
public interface ISndDataAccess
{
    void SetData<T>(string name, T value);

    T GetData<T>(string name);

    /// <summary>
    ///     安全读取数据，返回 <c>(是否找到, 值)</c>。
    ///     <para>
    ///         使用约束：始终先判断 <c>found</c>，再使用 <c>value</c>。
    ///         C# 泛型中 <c>T?</c> 对值类型（如 <c>int</c>、<c>float</c>）退化为非 nullable T，
    ///         未找到时 <c>value</c> 为 <c>default(T)</c>（如 0），不可通过 <c>??</c> 兜底。
    ///         对引用类型（如 <c>string</c>），未找到时 <c>value</c> 为 <c>null</c>。
    ///     </para>
    /// </summary>
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
