# Console

> [↑ 回到 Runtime](../README.md)

## 模块能力

Origo 的运行时控制台命令系统。提供命令解析（位置参数 + 命名参数）、命令路由（命令名 → 处理器）以及输入输出通道（队列 + 发布-订阅）。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [CommandHandlers](CommandHandlers/README.md) | 11 个内置命令处理器 | help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `OrigoConsole.cs` | 控制台门面：持有 Router + Input + OutputChannel + Parser |
| `ConsoleCommandRouter.cs` | 命令路由：命令名 → IConsoleCommandHandler 注册与查找 |
| `ConsoleCommandParser.cs` | 命令解析：字符串 → CommandInvocation（位置参数 + 命名参数）|
| `ConsoleCommandHandlerBase.cs` | 命令处理器基类：Name/HelpText/参数范围校验/执行 |
| `CommandInvocation.cs` | 命令调用模型：CommandName + PositionalArgs + NamedArgs |
| `IConsoleCommandHandler.cs` | 命令处理器接口：Name + HelpText + TryExecute |
| `ConsoleInputBuffer.cs` | IConsoleInputSource 实现：线程安全的命令输入队列，支持 Enqueue/TryDequeue/Clear |
| `ConsoleOutputChannel.cs` | IConsoleOutputChannel 实现：订阅/发布 |
| `ConsoleCommandHelper.cs` | internal 工具类：实体查找 TryFindEntity、黑板层解析、类型推断 |

## 命令生命周期

```
外部输入 (Godot 控制台 / TCP 桥接)
    │
    ▼
ConsoleInputBuffer.Enqueue(line)
    │
    ▼
OrigoConsole.ProcessPending()
    ├── TryDequeueCommand → line
    ├── ConsoleCommandParser.Parse(line)
    │   └── CommandInvocation { Name, PositionalArgs, NamedArgs }
    ├── ConsoleCommandRouter.TryExecute(invocation, outputChannel)
    │   └── handler.TryExecute(invocation, outputChannel)
    └── outputChannel.Publish(result)
```

## 设计原则

- **命名参数支持**：除位置参数外，支持 `key=value` 命名参数（如 `spawn name=x template=y`）。两种模式不可混用
- **参数校验前置**：`ConsoleCommandHandlerBase.TryExecute` 在执行前校验参数数量，失败时返回清晰错误
- **线程安全输入**：`ConsoleInputBuffer` 使用 `lock` 保护，支持 TCP 桥接线程并发入队
- **命令处理器异常立即传播**：`ProcessPending()` 不捕获命令处理器抛出的异常。若处理器执行中抛出异常（如 `InvalidOperationException`），异常直接传播到调用方，不降级为日志或错误消息。这确保开发阶段尽早暴露 bug。
- **输出通道监听器隔离**：`ConsoleOutputChannel.Publish()` 将每个订阅者的调用包裹在 try-catch 中。若单个监听器抛出异常，后续订阅者仍能收到该输出行。在所有订阅者调用完成后，第一个异常被重新抛出以保持 fail-fast。这确保输出不会因单个有缺陷的监听器而静默丢失。

---
[↑ 回到 Runtime](../README.md)
