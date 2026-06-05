using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     数据存取与订阅能力，从 <see cref="ISndEntity" /> 中拆分，遵循接口隔离原则。
///     <para>
///         订阅回调统一为 <c>(target, observer, oldValue, newValue)</c> —— 自订阅时 target == observer，
///         跨实体观察时 <c>observer</c> 为发起 <c>ObserveData</c> 的实体。
///         <see cref="Subscribe" /> 等价于 <c>ObserveData(this, ...)</c>，统一内部链路。
///     </para>
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
    ///     订阅本实体指定键的数据变更通知。
    ///     <para>
    ///         等价于 <c>ObserveData(this, name, callback, filter)</c>，走统一内部链路。
    ///         回调签名 <c>(target, observer, oldValue, newValue)</c>。
    ///         退订时须传入与订阅时相同的委托实例（方法引用），lambda 表达式每次编译产生不同实例，会导致退订失败。
    ///     </para>
    /// </summary>
    void Subscribe(string name, Action<ISndEntity, ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, ISndEntity, TypedData, TypedData, bool>? filter = null);

    /// <summary>
    ///     取消订阅指定键的数据变更通知。
    ///     <c>callback</c> 必须与 <see cref="Subscribe" /> 调用时的委托实例相同（方法引用）。
    /// </summary>
    void Unsubscribe(string name, Action<ISndEntity, ISndEntity, TypedData, TypedData> callback);
}
