<!-- docsync-pair: Origo.Core/Runtime/Console/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Console

> [↑ 回到 Runtime](../README.zh.md) · [↔ 工具契约: Origo.Core.Contracts/Runtime/Console](../../../Origo.Core.Contracts/Runtime/Console/README.zh.md)

## 模块能力

Origo 的运行时控制台命令系统实现。提供命令解析（位置参数 + 命名参数）、命令路由
（命令名 → 处理器）以及输入输出通道（队列 + 发布-订阅）。命令处理器接口、调用模型与
参数校验基类位于 [Origo.Core.Contracts/Runtime/Console](../../../Origo.Core.Contracts/Runtime/Console/README.zh.md)。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [CommandHandlers](CommandHandlers/README.zh.md) | 11 个基础命令处理器 + 5 个持久化命令处理器 | OrigoConsole 注册 help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy；SndContext 注册 list_saves / save / load / delete_save / switch_level |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `OrigoConsole.cs` | 控制台门面：持有 Router + Input + OutputChannel + Parser |
| `ConsoleCommandRouter.cs` | 命令路由：命令名 → IConsoleCommandHandler 注册与查找 |
| `ConsoleCommandParser.cs` | 命令解析：字符串 → CommandInvocation（位置参数 + 命名参数），`internal` |
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
IOrigoFrameDriver.DriveFrame(delta) → OrigoConsole.ProcessPending() (internal)
    ├── TryDequeueCommand → line
    ├── ConsoleCommandParser.Parse(line)
    │   └── CommandInvocation { Command, PositionalArgs, NamedArgs }
    ├── ConsoleCommandRouter.TryExecute(invocation, outputChannel)
    │   └── handler.TryExecute(invocation, outputChannel)
    └── outputChannel.Publish(result)
```

## 设计原则

- **命名参数支持**：除位置参数外，支持 `key=value` 命名参数（如 `spawn name=x template=y`）。两种模式不可混用。重复的命名参数拒绝解析并返回错误（fail-fast）。
- **参数校验前置**：校验基类位于 Contracts，命令处理器在执行前统一校验参数数量。
- **线程安全输入**：`ConsoleInputBuffer` 使用 `lock` 保护，支持 TCP 桥接线程并发入队。
- **命令处理器异常立即传播**：internal `ProcessPending()` 不捕获命令处理器抛出的异常，异常直接传播到帧驱动调用方。
- **命令名唯一性**：`ConsoleCommandRouter.Register` 要求命令名全局唯一，重复注册抛 `InvalidOperationException`。
- **输出通道监听器隔离**：`ConsoleOutputChannel.Publish()` 隔离单个订阅者异常；所有订阅者执行完后重新抛出第一个异常，保持 fail-fast，同时保证输出不因单个缺陷监听器静默丢失。

---
[↑ 回到 Runtime](../README.zh.md)
