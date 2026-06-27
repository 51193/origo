# StateMachine

> [↑ 回到 Origo.Core](../README.md) · [↔ 相关测试: StateMachine](../../Origo.Core.Tests/StateMachine.md)

## 概述

`IStateMachine` 接口的完整实现。提供字符串栈状态机 (`StackStateMachine`)、状态机策略基类 (`StateMachineStrategyBase`)、策略上下文结构体、以及持久化模型。状态机本身只存储字符串标识符栈，具体行为由关联的 Push/Pop 策略定义。

## 包含文件

| 文件 | 职责 |
|------|------|
| `StackStateMachine.cs` | 字符串栈状态机完整实现，支持 Push/Pop/Peek/持久化 |
| `StateMachineStrategyBase.cs` | 状态机策略基类（OnPushRuntime / OnPopRuntime 等钩子）|
| `StateMachineStrategyContext.cs` | 单次回调的栈上下文 struct（BeforeTop / AfterTop）|
| `StateMachinePersistenceModels.cs` | 持久化模型：StateMachineContainerPayload |

## 模块详解

### StackStateMachine

核心状态机，维护 `List<string> _stack`，通过注入的 `SndStrategyPool` 持有 Push/Pop 策略引用。关键行为：

| 操作 | Push 策略钩子 | Pop 策略钩子 | 时机 |
|------|-------------|-------------|------|
| `Push(value)` | `OnPushRuntime` | - | 运行时入栈后 |
| `TryPopRuntime()` | - | `OnPopRuntime` | 运行时出栈前 |
| `TryPopOnQuit()` | - | `OnPopBeforeQuit` | 退出流程出栈前 |
| `FlushAfterLoad()` | `OnPushAfterLoad` | - | 读档恢复后，自底向顶 |

每个钩子接收 `(MachineKey, BeforeTop, AfterTop)` 上下文，明确告知策略操作前后的栈顶变化。

### StateMachineStrategyBase

继承自 `BaseStrategy`，提供 4 个虚方法钩子。所有方法参数为 `StateMachineStrategyContext` + `IStateMachineContext`，前后台共用相同抽象。

### StateMachineStrategyContext

`readonly struct`，不可变值类型。包含：
- `MachineKey`：状态机在容器中的标识
- `BeforeTop`：操作前栈顶（空栈时 null）
- `AfterTop`：操作后栈顶（栈清空时 null）

### 持久化模型

`StateMachineContainerPayload` 包含一组 `StateMachineEntryPayload`（每个条目：key + pushIndex + popIndex + stack 快照列表）。

## 设计决策

### 为什么 StackStateMachine 同时持有 Push 和 Pop 策略引用

两个策略在构造时同时从池中获取，引用计数各 +1。`Dispose()` 时各 -1。这确保状态机生命周期内策略不被回收，且引用计数精确匹配池的重用机制。

### 为什么 RestoreStackWithoutHooks + FlushAfterLoad 分两步

存档恢复需要两个阶段：(1) 无钩子恢复栈内容（避免恢复过程中触发副作用），(2) 栈结构调整完成后，按顺序重放 AfterLoad 钩子。如果合并为一步，钩子会在栈不完整时触发，导致策略拿到不正确的上下文。

### 为什么策略钩子参数包含 BeforeTop 和 AfterTop

策略可能需要感知状态迁移方向（例如从 "menu" 切换到 "gameplay" 时执行初始化）。仅告知当前栈值不够，需要"从哪里来、到哪里去"的迁移信息。

---
[↑ 回到 Origo.Core](../README.md)
