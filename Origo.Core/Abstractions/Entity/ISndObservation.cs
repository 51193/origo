using System;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Entity;

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
