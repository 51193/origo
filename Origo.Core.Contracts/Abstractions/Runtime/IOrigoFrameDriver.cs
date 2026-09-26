namespace Origo.Core.Abstractions.Runtime;

/// <summary>
///     Host environment frame driver. Adapter layers call <see cref="DriveFrame"/> each
///     simulation tick instead of directly flushing deferred pipelines, processing entities,
///     or pumping console. Core owns the internal ordering: entity processing → business queue
///     → kill pending → system queue → console processing. The Adapter merely signals that a
///     frame boundary has arrived along with its delta time.
/// </summary>
public interface IOrigoFrameDriver
{
    /// <summary>Signals that a frame boundary has arrived with its delta time.</summary>
    /// <param name="delta">The elapsed time in seconds since the previous frame.</param>
    void DriveFrame(double delta);
}
