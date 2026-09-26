using System;
using Origo.Core.Abstractions.Logging;

namespace Origo.Core.Logging;

/// <summary>
///     Generic logger adapter: derives the log tag from
///     <typeparamref name="T" />'s type name and delegates to
///     an underlying <see cref="ILogger" />.
/// </summary>
public sealed class Logger<T> : ILogger<T>
{
    private readonly ILogger _inner;

    /// <summary>Wraps an underlying logger to add an auto-derived category tag.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner" /> is null.</exception>
    public Logger(ILogger inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Logs a message with the category type name as the tag.</summary>
    public void Log(LogLevel level, string message)
        => _inner.Log(level, typeof(T).Name, message);

    void ILogger.Log(LogLevel level, string tag, string message)
        => _inner.Log(level, tag, message);
}
