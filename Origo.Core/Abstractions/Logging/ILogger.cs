namespace Origo.Core.Abstractions.Logging;

public enum LogLevel
{
    Debug,
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

/// <summary>
///     带类型感知的日志接口。标签自动取自 <typeparamref name="TCategory"/> 的类型名，
///     无需调用方手动指定 tag 字符串。
///     同时继承 <see cref="ILogger"/> 以兼容需要手动 tag 的场景。
/// </summary>
public interface ILogger<out TCategory> : ILogger
{
    void Log(LogLevel level, string message);
}
