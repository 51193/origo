using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     <see cref="ISndEntity" /> 的组成接口，提供跨实体观察与生命周期订阅能力。
///     外部代码应通过 <see cref="ISndEntity" /> 使用，无需直接依赖此接口。
/// </summary>
public interface ISndObservation
{
    void ObserveData(ISndEntity target, string dataName,
        Action<ISndEntity, ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, ISndEntity, TypedData, TypedData, bool>? filter = null);

    void UnobserveData(ISndEntity target, string dataName,
        Action<ISndEntity, ISndEntity, TypedData, TypedData> callback);

    void ObserveLifecycle(ISndEntity target,
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);

    void UnobserveLifecycle(ISndEntity target,
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);
}
