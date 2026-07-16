namespace Origo.Core.Abstractions.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
///     Engine-agnostic base logging interface.
///     The host environment (e.g., Godot, console application) provides
///     the actual implementation.
/// </summary>
public interface ILogger
{
    void Log(LogLevel level, string tag, string message);
}

/// <summary>
///     Type-aware logging interface. The tag is automatically derived from
///     the type name of <typeparamref name="TCategory"/>, eliminating manual
///     tag string specification by callers. Also inherits <see cref="ILogger"/>
///     for compatibility with manual-tag scenarios.
/// </summary>
public interface ILogger<out TCategory> : ILogger
{
    void Log(LogLevel level, string message);
}
