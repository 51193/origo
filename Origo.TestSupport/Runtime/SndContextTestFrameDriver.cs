using System;
using Origo.Core.Snd;

namespace Origo.TestSupport;

/// <summary>
///     Test-side deferred-queue flush for full SndContext workflow tests.
///     The old public <c>ISndDeferredActions.FlushDeferredActionsForCurrentFrame</c>
///     business surface is sealed; tests reach the same framework-internal
///     pipeline through InternalsVisibleTo without processing entities or
///     pumping the console.
/// </summary>
internal static class SndContextTestFrameDriver
{
    internal static void FlushFrame(this SndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Runtime.FlushEndOfFrameDeferred();
    }
}
