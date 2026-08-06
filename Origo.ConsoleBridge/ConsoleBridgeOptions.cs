namespace Origo.ConsoleBridge;

/// <summary>Configuration options for the TCP console bridge server.</summary>
public sealed class ConsoleBridgeOptions
{
    /// <summary>The default TCP listen port (9876).</summary>
    public const int DefaultPort = 9876;

    /// <summary>The TCP port to listen on; defaults to <see cref="DefaultPort" />.</summary>
    public int Port { get; set; } = DefaultPort;
}
