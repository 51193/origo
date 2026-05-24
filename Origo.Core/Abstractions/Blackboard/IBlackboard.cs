using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Blackboard;

/// <summary>
///     通用键值黑板接口，面向 Core 层的全局/进度/会话级共享状态。
///     内部使用 <see cref="TypedData" /> 保留类型信息，确保序列化/反序列化后类型不丢失。
/// </summary>
public interface IBlackboard
{
#pragma warning disable CA1716 // Identifiers should not match keywords — Set is an intentional API name for this blackboard
    void Set<T>(string key, T value);
#pragma warning restore CA1716

    (bool found, T value) TryGet<T>(string key);

    void Clear();

    IReadOnlyCollection<string> GetKeys();

    /// <summary>
    ///     导出全部条目（带类型信息），用于序列化持久化。
    /// </summary>
    IReadOnlyDictionary<string, TypedData> SerializeAll();

    /// <summary>
    ///     从带类型信息的字典恢复全部条目，替换当前内容。
    /// </summary>
    void DeserializeAll(IReadOnlyDictionary<string, TypedData> data);
}
