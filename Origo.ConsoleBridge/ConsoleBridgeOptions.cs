namespace Origo.ConsoleBridge;

/// <summary>Configuration options for the TCP console bridge server.</summary>
public sealed class ConsoleBridgeOptions
{
    /// <summary>The default TCP listen port (9876).</summary>
    public const int DefaultPort = 9876;

    /// <summary>
    ///     The default send timeout (milliseconds) for console output writes.
    /// </summary>
    public const int DefaultOutputSendTimeoutMs = 100;

    /// <summary>The TCP port to listen on; defaults to <see cref="DefaultPort" />.</summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>
    ///     The maximum time (milliseconds) a single console output write may
    ///     block before the connection is considered dead and detached.
    ///     Output writes run on the game frame thread; a client that stops
    ///     reading would otherwise fill the TCP send buffer and stall the
    ///     frame loop indefinitely. Undelivered lines are buffered and
    ///     replayed to the next connection. Defaults to
    ///     <see cref="DefaultOutputSendTimeoutMs" />.
    /// </summary>
    public int OutputSendTimeoutMs { get; set; } = DefaultOutputSendTimeoutMs;
}
