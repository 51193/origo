using System;

namespace Origo.Core.Abstractions.Entity;

public interface ISndObservation
{
    void ObserveData(ISndEntity target, string dataName,
        Action<ISndEntity, ISndEntity, object?, object?> callback,
        Func<ISndEntity, ISndEntity, object?, object?, bool>? filter = null);

    void UnobserveData(ISndEntity target, string dataName,
        Action<ISndEntity, ISndEntity, object?, object?> callback);

    void ObserveLifecycle(ISndEntity target,
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);

    void UnobserveLifecycle(ISndEntity target,
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback);
}
