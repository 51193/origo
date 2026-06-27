# Logging

> [↑ 回到 Origo.GodotAdapter](../README.md) · [↔ Core 抽象: Abstractions/Logging](../../Origo.Core/Abstractions/Logging/README.md)

## 概述

`ILogger` 接口的 Godot 实现。通过构造函数注入输出委托（delegate），将日志消息转发到 Godot 引擎的日志系统（`GD.Print` / `GD.PushWarning` / `GD.PushError`）。支持最低日志级别过滤。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GodotLogger.cs` | Godot 日志实现，委托注入 + 级别过滤 |

## 实现详解

```csharp
public sealed class GodotLogger : ILogger
{
    private readonly Action<LogLevel, string, string>? _handler;
    private readonly LogLevel _minimumLevel;

    public GodotLogger(
        Action<LogLevel, string, string>? handler = null,
        LogLevel minimumLevel = LogLevel.Info);

    public void Log(LogLevel level, string tag, string message)
    {
        if (level < _minimumLevel) return;
        _handler?.Invoke(level, tag, message);
    }
}
```

不包含任何静态状态。实际的输出行为（格式化、级别路由）由外部委托控制。`minimumLevel` 默认为 `Info`，低于此级别的 `Debug` 消息不触发委托。典型用法（来自 `OrigoAutoHost`）：

```csharp
new GodotLogger((level, tag, message) =>
{
    switch (level)
    {
        case LogLevel.Warning: GD.PushWarning($"[{tag}] {message}"); break;
        case LogLevel.Error: GD.PushError($"[{tag}] {message}"); break;
        default: GD.Print($"[{tag}] {message}"); break;
    }
});
```

## 设计决策

### 为什么用委托而非直接硬编码 GD.Print

不同使用场景可能需要不同的日志路由（Godot 编辑器控制台、文件日志、远程日志）。委托注入让调用方决定输出策略，`GodotLogger` 本身只负责接口适配。

### 为什么 handler 可为 null

启动早期（构造 `OrigoAutoHost` 时）`GodotLogger` 实例先创建，但 Godot 的 `GD` API 在特定初始化阶段可能不可用。允许 null handler 使日志在"静默"和"活跃"之间切换，无需重建实例。

### 为什么不在 Log 方法中做格式化

格式化（`[tag] message`）的责任留给委托实现。Core 层的 `LogMessageBuilder` 已经处理结构化消息构建。在 `GodotLogger` 中重复格式化会导致不一致的输出风格。

### 为什么默认最低级别是 Info 而非 Debug

`Debug` 级别的日志主要用于开发期的细节诊断（策略实例创建/释放、实体生命周期挂钩等）。在生产环境（Demo 运行、正式发布）中，这些消息数量大、信息密度低，默认关闭可避免日志泛滥。需要诊断时显式传入 `LogLevel.Debug` 即可。

---

[↑ 回到 Origo.GodotAdapter](../README.md)
