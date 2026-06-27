# Scheduling

> [↑ 回到 Origo.Core](../README.md) · [↔ 抽象: Abstractions/Runtime](../Abstractions/Runtime/README.md)

## 概述

`IScheduler` 接口的具体实现。提供基于 `ConcurrentActionQueue` 的简单调度器，以及线程安全的延迟执行队列。宿主环境负责在合适时机调用 `Tick` 执行排队动作。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ActionScheduler.cs` | `IScheduler` 的 internal 实现，包装 `ConcurrentActionQueue` |
| `ConcurrentActionQueue.cs` | 线程安全的延迟执行队列，批次 drain，支持重入保护 |

## 实现详解

### ActionScheduler

薄包装层，将 `ConcurrentActionQueue` 适配为 `IScheduler` 接口：
- `Enqueue(action)` → 入队
- `Tick()` → `ExecuteAll()`，返回本帧执行动作数
- `Clear()` → 清空队列

### ConcurrentActionQueue

核心队列实现，使用 `List<Action>` + `lock` 确保线程安全：
- **批次 drain**：每轮加锁取出当前所有动作快照，清空队列，释放锁后逐个 invoke
- **重入保护**：若动作内部又 enqueue 新动作，下一次 while 循环继续 drain。`MaxReentrantDrainDepth=100` 防止无限循环
- **异常处理**：单个动作异常时记录 Error 日志后重新抛出（let-it-crash 语义）

## 设计决策

### 为什么使用快照式 drain 而非边取边执行

边取边执行（如 `while(Dequeue()) invoke()`）需要持续持锁，且执行期间无法入队新动作。快照式先把所有待执行动作 copy 出锁区，释放锁后逐个 invoke，既减少锁竞争，又允许动作内部入队新动作。

### 为什么 ActionScheduler 是 internal

调度器仅在 Runtime 层内部使用。外部代码通过上层 API（如 `SndContext` 的 `EnqueueBusinessDeferred`）间接使用调度能力，不直接与 `ActionScheduler` 交互。

### 为什么异常时不吞掉而是重新抛出

延迟队列中的动作是帧模型的一部分。若一个动作失败，系统应崩溃而非静默跳过，避免业务逻辑在未知损坏状态下继续执行。日志记录异常详情，然后 throw。

---
[↑ 回到 Origo.Core](../README.md)
