using System;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Logging;

namespace Origo.ConsoleBridge;

/// <summary>
///     ILogger 装饰器：将日志同时转发到 Console 输出通道，供外部 Agent 消费。
/// </summary>
public sealed class LogRelay : ILogger
{
    private readonly ILogger _inner;
    private readonly LogLevel _minLevel;
    private readonly IConsoleOutputChannel _output;

    public LogRelay(ILogger inner, IConsoleOutputChannel output, LogLevel minLevel = LogLevel.Info)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(output);
        _inner = inner;
        _output = output;
        _minLevel = minLevel;
    }

    public void Log(LogLevel level, string tag, string message)
    {
        _inner.Log(level, tag, message);
        if (level >= _minLevel)
            _output.Publish($"[{level.ToString().ToUpperInvariant()}][{tag}] {message}");
    }
}
