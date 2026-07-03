using System;
using Origo.Core.Abstractions.Logging;

namespace Origo.Core.Logging;

public sealed class Logger<T> : ILogger<T>
{
    private readonly ILogger _inner;

    public Logger(ILogger inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public void Log(LogLevel level, string message)
        => _inner.Log(level, typeof(T).Name, message);

    void ILogger.Log(LogLevel level, string tag, string message)
        => _inner.Log(level, tag, message);
}
