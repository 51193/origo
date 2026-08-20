using System;
using System.Threading;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Snd.Companions;

/// <summary>Deferred action scheduling and persistence tracking for <see cref="SndContext" />.</summary>
internal sealed class SndContextDeferredActions(SndContext owner) : ISndDeferredActions
{
    /// <inheritdoc/>
    public void EnqueueBusinessDeferred(Action action) => owner.Runtime.EnqueueBusinessDeferred(action);

    /// <inheritdoc/>
    public int GetPendingPersistenceRequestCount() =>
        Interlocked.CompareExchange(ref owner._pendingPersistenceRequests, 0, 0);
}
