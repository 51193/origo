using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Snd.Entity;

internal interface ISndEntityRawSubscription
{
    void SubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback,
        Func<ISndEntity, TypedData, TypedData, bool>? filter);

    void UnsubscribeDataRaw(string name, Action<ISndEntity, TypedData, TypedData> callback);
}
