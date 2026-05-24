using Origo.Core.Abstractions.Logging;

namespace Origo.Core.Logging;

/// <summary>
///     无输出日志实现，用于测试或不需要日志的调用方。
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
