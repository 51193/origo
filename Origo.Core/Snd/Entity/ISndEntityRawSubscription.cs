using System;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Entity;

internal interface ISndEntityRawSubscription
{
    void SubscribeDataRaw(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter);

    void UnsubscribeDataRaw(string name, Action<ISndEntity, object?, object?> callback);

    void SubscribeLifecycleRaw(Action<ISndEntity, EntityLifecycleEvent> callback);

    void UnsubscribeLifecycleRaw(Action<ISndEntity, EntityLifecycleEvent> callback);
}
