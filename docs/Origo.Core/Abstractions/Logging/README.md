# Logging (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: Logging](../../Logging/README.md)

## 概述

定义引擎无关的基础日志接口 `ILogger` 和日志级别枚举 `LogLevel`。Core 层只关心日志的消息内容和级别，不关心输出目的地（Godot 控制台、文件、网络等）。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ILogger.cs` | 定义 `ILogger` 接口和 `LogLevel` 枚举 |

## 接口详细

### LogLevel

| 级别 | 用途 |
|------|------|
| `Debug` | 单次细节操作（I/O、文件读写），描述操作意图与结果 |
| `Info` | 关键节点（阶段切换、外部调用完成） |
| `Warning` | 不合预期但不影响系统运行，存在出现问题可能 |
| `Error` | 崩溃级错误，系统无法继续正常运作 |

### ILogger

| 成员 | 说明 |
|------|------|
| `Log(level, tag, message)` | 记录一条带级别、标签和正文的日志 |

## 设计决策

### 为什么日志级别和接口放在同一个文件

`LogLevel` 作为 `ILogger` 的输入参数，两者耦合紧密。拆分为两个文件不会带来复用价值（`LogLevel` 不会被其他接口独立使用），反而增加导航成本。

### 为什么不提供格式化方法

Format 和 interpolation 是调用方的关注点。Core 层的 `LogMessageBuilder`（见 [Logging 实现](../../Logging/README.md)）已提供结构化消息构建能力，`ILogger` 保持接收纯字符串的最小接口。

---
[↑ 回到 Abstractions](../README.md)
