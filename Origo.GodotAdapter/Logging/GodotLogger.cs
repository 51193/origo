using System;
using Origo.Core.Abstractions.Logging;

namespace Origo.GodotAdapter.Logging;

/// <summary>
///     Godot 环境下的日志实现，通过构造函数注入具体的输出委托。
///     不再使用静态状态，生命周期由持有者管理。
/// </summary>
public sealed class GodotLogger : ILogger
{
    private readonly Action<LogLevel, string, string>? _handler;

    public GodotLogger(Action<LogLevel, string, string>? handler = null)
    {
        _handler = handler;
    }

    public void Log(LogLevel level, string tag, string message) => _handler?.Invoke(level, tag, message);
}
