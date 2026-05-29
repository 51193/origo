namespace Origo.ConsoleBridge;

public sealed class ConsoleBridgeOptions
{
    public const int DefaultPort = 9876;

    public int Port { get; set; } = DefaultPort;
}
