using Origo.Core.Abstractions.Logging;

namespace Origo.Core.Logging;

/// <summary>
///     No-op logging implementation for testing or callers that do not require logging.
/// </summary>
public sealed class NullLogger : ILogger
{
    private NullLogger()
    {
    }

    public static NullLogger Instance { get; } = new();

    public void Log(LogLevel level, string tag, string message)
    {
    }
}
