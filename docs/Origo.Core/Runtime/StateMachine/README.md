# StateMachine (Runtime)

> [↑ 回到 Runtime](../README.md) · [↔ 实现: StateMachine](../../StateMachine/README.md)

## 概述

状态机容器的运行时封装。`StateMachineContainer` 实现 `IStateMachineContainer` 接口，按 key 管理多个 `StackStateMachine` 实例，负责创建、查找、移除、序列化和反序列化。生命周期与策略池引用计数对齐，通过 `IStateMachineContext` 而非具体上下文类型运作。

## 包含文件

| 文件 | 职责 |
|------|------|
| `StateMachineContainer.cs` | 状态机容器：CreateOrGet / TryGet / Remove / Clear / 序列化 / 反序列化 |

## 模块详解

### StateMachineContainer : IStateMachineContainer

- 实现 `IStateMachineContainer` 接口（定义于 Abstractions 层）
- 对 `ISessionRun` 暴露为 `IStateMachineContainer`，对 Runtime 层内部暴露具体 `StateMachineContainer`（含 `FlushAllAfterLoad`、`SerializeToNode` 等内部方法）
- `ForEachMachine` 统一了 `FlushAllAfterLoad`/`PopAllRuntime`/`PopAllOnQuit` 的迭代模式，消除重复

**核心操作**：

| 操作 | 说明 |
|------|------|
| `CreateOrGet(key, pushIndex, popIndex)` | 若 key 不存在则创建新 StackStateMachine；若已存在但策略索引不同则抛异常 |
| `TryGet(key)` | 按 key 查找已有状态机 |
| `Remove(key)` | 按 key 移除并 Dispose 状态机 |
| `Clear()` | 释放所有状态机并清空容器 |
| `SerializeToNode(registry)` | 序列化全部机器为 DataSourceNode |
| `DeserializeFromNode(node, registry)` | 从 DataSourceNode 恢复全部状态机（原子替换） |

**批量操作**：

| 操作 | 说明 |
|------|------|
| `FlushAllAfterLoad()` | 按插入顺序对所有机器执行 FlushAfterLoad |
| `PopAllRuntime()` | 按插入顺序逐个弹空所有栈（运行时语义）|
| `PopAllOnQuit()` | 按插入顺序逐个弹空所有栈（退出语义）|

**反序列化策略**：
1. 解析 payload，校验所有条目（key 唯一、索引非空、stack 非 null）
2. 创建新状态机并 RestoreStackWithoutHooks
3. 异常时 Dispose 已创建的新机器
4. 成功时原子替换旧机器（清空旧的 → 填入新的 → Dispose 旧的）

## 设计决策

### 为什么序列化时保留 key 顺序

存档恢复后需要按原始顺序重放钩子（`FlushAllAfterLoad`）。如果顺序丢失，推栈/弹栈的迁移链条可能断裂。`_machineOrder` 追踪插入顺序。

### 为什么反序列化使用原子替换

如果只替换部分状态机，新旧状态机可能引用相同的策略池引用，造成引用计数混乱。全量替换确保每个状态机的引用计数一致。

### 为什么 CreateOrGet 对策略索引不匹配抛异常

状态机的 Push/Pop 策略索引是其行为的核心定义。如果创建时使用不同的索引，行为语义完全不同，此时静默返回已有实例是危险的。

---
[↑ 回到 Runtime](../README.md)
