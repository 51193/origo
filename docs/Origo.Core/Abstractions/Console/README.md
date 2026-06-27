# Console (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: Runtime/Console](../../Runtime/Console/README.md)

## 概述

定义 Core 层与外部控制台系统之间的输入输出抽象。Core 不直接依赖具体的控制台实现（如 Godot 调试器、TCP 桥接），只通过这两个接口完成双向通信：适配层通过 `Enqueue` 投递命令，Core 通过 `TryDequeueCommand` 按帧消费。

## 包含文件

| 文件 | 职责 |
|------|------|
| `IConsoleInputSource.cs` | 双向命令输入抽象：适配层投递命令（Enqueue），Core 按帧消费（TryDequeueCommand），无 UI/线程依赖 |
| `IConsoleOutputChannel.cs` | 发布-订阅式输出通道，支持多监听者 |

## 接口详细

### IConsoleInputSource

| 成员 | 说明 |
|------|------|
| `TryDequeueCommand(out string? line)` | 非阻塞取出一行待解析命令；队列空时返回 false |
| `Enqueue(string line)` | 向队列追加命令行；空白行被自动忽略 |
| `Clear()` | 清空队列中所有待处理命令 |

### IConsoleOutputChannel

| 成员 | 说明 |
|------|------|
| `Subscribe(Action<string>)` | 注册输出监听器，返回订阅 ID |
| `Unsubscribe(long subscriptionId)` | 取消订阅；ID 不存在返回 false |
| `Publish(string line)` | 发布一条输出消息给所有监听者 |

## 设计决策

### 为什么 Enqueue 和 Clear 也在接口上

原 `IConsoleInputSource` 仅暴露 `TryDequeueCommand`，适配层和 ConsoleBridge 需依赖具体类 `ConsoleInputBuffer` 调用 `Enqueue` 和 `Clear`，造成抽象泄漏。将这两个方法提升到接口后，所有外部消费者可通过 `IConsoleInputSource` 完成投递、消费和清空的全生命周期操作，消除对具体类的编译期依赖。

### 为什么输入用轮询而非事件

Origo 采用单线程帧循环模型。轮询式 `TryDequeueCommand` 可在帧内的确定时机处理命令，避免事件回调引入的不可预测执行顺序和潜在的递归问题。

### 为什么输出用发布-订阅

控制台输出可能有多个消费者（日志文件写入、屏幕渲染、远程转发）。发布-订阅模式让 Core 无需知道消费者数量和类型。

### 为什么输出通道不保留历史

`IConsoleOutputChannel` 无本地缓冲，只负责分发。历史管理由具体消费者自行决定，避免接口膨胀。

---
[↑ 回到 Abstractions](../README.md)
