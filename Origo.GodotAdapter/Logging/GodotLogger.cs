using System;
using Origo.Core.Abstractions.Logging;

namespace Origo.GodotAdapter.Logging;

public sealed class GodotLogger : ILogger
{
    private readonly Action<LogLevel, string, string>? _handler;
    private readonly LogLevel _minimumLevel;

    public GodotLogger(Action<LogLevel, string, string>? handler = null, LogLevel minimumLevel = LogLevel.Info)
    {
        _handler = handler;
        _minimumLevel = minimumLevel;
    }

    public void Log(LogLevel level, string tag, string message)
    {
        if (level < _minimumLevel) return;
        _handler?.Invoke(level, tag, message);
    }
}
