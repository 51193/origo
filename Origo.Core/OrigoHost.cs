using System;
using System.Threading;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Kernel.Ports;
using Origo.Core.Snd;

namespace Origo.Core;

/// <summary>
///     Core shell host facade. Creates a kernel runtime/SND context behind
///     stable Contracts interfaces, so consumers that reference only the Core
///     shell package can compile and run a Core workflow without kernel compile
///     assets.
/// </summary>
public sealed class OrigoHost : IOrigoFrameDriver
{
    private readonly ISndContext _context;
    private int _disposeState;

    private OrigoHost(IOrigoRuntime runtime, ISndContext context)
    {
        Runtime = runtime;
        _context = context;
    }

    /// <summary>Stable runtime surface backed by the kernel implementation.</summary>
    public IOrigoRuntime Runtime { get; }

    /// <summary>Stable SND context used for bootstrap and business capabilities.</summary>
    public ISndContext Context => _context;

    /// <summary>
    ///     Creates a host using the supplied stable options. The concrete
    ///     runtime, SND world, and SND context are constructed by an internal
    ///     kernel port.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is null.</exception>
    public static OrigoHost Create(OrigoHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var bundle = HostKernelPort.Create(options);
        return new OrigoHost(bundle.Runtime, bundle.Context);
    }

    /// <summary>
    ///     Runs the standard SND bootstrap sequence. Call exactly once when the
    ///     host should load its configured entry scene.
    /// </summary>
    public void Bootstrap() => _context.Bootstrap();

    /// <summary>Drives one runtime frame through the stable frame-driver contract.</summary>
    public void DriveFrame(double delta) => Runtime.DriveFrame(delta);

    /// <summary>
    ///     Stops the active progress run, runs session/progress quit hooks, and
    ///     cleans the temporary <c>current/</c> directory. Idempotent.
    /// </summary>
    public void Dispose()
    {
        // 0 = live, 1 = shutdown in progress, 2 = successfully shut down.
        // A failed attempt returns to 0 so the caller can retry after fixing
        // the failing teardown dependency; a successful attempt is terminal.
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            return;

        try
        {
            _context.Shutdown();
        }
        catch
        {
            Interlocked.Exchange(ref _disposeState, 0);
            throw;
        }

        Interlocked.Exchange(ref _disposeState, 2);
    }
}
