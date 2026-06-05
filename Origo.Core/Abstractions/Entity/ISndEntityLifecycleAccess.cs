using System;

namespace Origo.Core.Abstractions.Entity;

public interface ISndEntityLifecycleAccess
{
    void SubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);

    void UnsubscribeLifecycle(Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);
}
