namespace Origo.ConsoleBridge;

/// <summary>Configuration options for the TCP console bridge server.</summary>
public sealed class ConsoleBridgeOptions
{
    public const int DefaultPort = 9876;

    public int Port { get; set; } = DefaultPort;
}
