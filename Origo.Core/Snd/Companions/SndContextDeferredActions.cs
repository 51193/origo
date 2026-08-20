using System;
using System.Threading;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Snd.Companions;

/// <summary>Deferred action scheduling and frame flush for <see cref="SndContext" />.</summary>
internal sealed class SndContextDeferredActions(SndContext owner) : ISndDeferredActions
{
    public void EnqueueBusinessDeferred(Action action) => owner.Runtime.EnqueueBusinessDeferred(action);

    public int GetPendingPersistenceRequestCount() =>
        Interlocked.CompareExchange(ref owner._pendingPersistenceRequests, 0, 0);
}
