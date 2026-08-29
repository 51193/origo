using System;
using Origo.Core.Snd;

namespace Origo.TestSupport;

/// <summary>
///     Test-side deferred-queue flush for full SndContext workflow tests.
///     Frame flushing is framework orchestration and is not exposed on
///     <see cref="ISndDeferredActions" />; tests reach the framework-internal
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
