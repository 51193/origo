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

    /// <summary>The shared no-op logger instance.</summary>
    public static NullLogger Instance { get; } = new();

    /// <summary>No-op; discards all messages.</summary>
    public void Log(LogLevel level, string tag, string message)
    {
    }
}
