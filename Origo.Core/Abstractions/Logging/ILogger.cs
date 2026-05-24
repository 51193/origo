namespace Origo.Core.Abstractions.Logging;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
///     提供与具体引擎无关的基础日志接口。
///     由宿主环境（例如 Godot、控制台应用等）提供实际实现。
/// </summary>
public interface ILogger
{
    void Log(LogLevel level, string tag, string message);
}
