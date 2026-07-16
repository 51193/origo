using System;
using Origo.Core.Abstractions.Logging;

namespace Origo.GodotAdapter.Logging;

/// <summary>
///     Godot engine adapter for <see cref="ILogger" />. Accepts a handler
///     delegate and a minimum log level. Messages below the minimum level
///     are silently dropped.
/// </summary>
public sealed class GodotLogger : ILogger
{
    private readonly Action<LogLevel, string, string> _handler;
    private readonly LogLevel _minimumLevel;

    public GodotLogger(Action<LogLevel, string, string> handler, LogLevel minimumLevel = LogLevel.Info)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _minimumLevel = minimumLevel;
    }

    public void Log(LogLevel level, string tag, string message)
    {
        if (level < _minimumLevel) return;
        _handler.Invoke(level, tag, message);
    }
}
