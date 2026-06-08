using System;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     <see cref="ISndEntity" /> 的组成接口，提供实体生命周期事件订阅能力。
///     外部代码应通过 <see cref="ISndEntity" /> 使用，无需直接依赖此接口。
/// </summary>
public interface ISndEntityLifecycleAccess
{
    void SubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);

    void UnsubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);
}
