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

    /// <summary>
    ///     Creates a logger that forwards messages to <paramref name="handler" />,
    ///     dropping messages below <paramref name="minimumLevel" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler" /> is null.</exception>
    public GodotLogger(Action<LogLevel, string, string> handler, LogLevel minimumLevel = LogLevel.Info)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _minimumLevel = minimumLevel;
    }

    /// <summary>Forwards a message to the handler when its level meets the minimum threshold.</summary>
    public void Log(LogLevel level, string tag, string message)
    {
        if (level < _minimumLevel) return;
        _handler.Invoke(level, tag, message);
    }
}
