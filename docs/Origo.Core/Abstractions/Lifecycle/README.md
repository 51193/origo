# Lifecycle (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: Lifecycle](../../Runtime/Lifecycle/README.md)

## 概述

定义会话管理的抽象接口。`ISessionManager` 和 `ISessionRun` 为策略层提供了与具体实现解耦的会话访问能力。这两个接口位于 Abstractions 层，确保 `ISndSessionAccess` 不依赖 Runtime 层。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ISessionManager.cs` | 会话管理器接口：创建/销毁/查找会话，管理前台与后台会话的完整生命周期 |
| `ISessionRun.cs` | 会话运行时接口：SessionBlackboard + SceneHost + LevelId + IsFrontSession + 状态机容器 |

## ISessionManager 成员

| 成员 | 说明 |
|------|------|
| `ForegroundKey` | 前台会话在管理器中的保留键（常量 `"__foreground__"`） |
| `CanCreateSessions` | 当前是否可以创建会话。`EmptySessionManager` 返回 `false`。调用方应先检查此属性而非依赖异常捕获 |
| `ForegroundSession` | 当前前台会话；无活动前台会话时为 null |
| `Keys` | 所有已挂载会话的键列表 |
| `TryGet(key)` | 按键获取会话 |
| `Contains(key)` | 检查指定键的会话是否存在 |
| `CreateBackgroundSession(key, levelId, syncProcess)` | 创建后台关卡会话并自动挂载 |
| `DestroySession(key)` | 销毁指定键的会话（Dispose 并从管理器移除） |
| `ProcessAllSessions(delta, includeForeground)` | 对所有配置为参与 Process 的会话执行帧更新 |

## ISessionRun 成员

| 成员 | 说明 |
|------|------|
| `SessionBlackboard` | 会话级黑板，隔离于其他会话 |
| `SceneHost` | 当前会话的 SND 场景宿主（前台和后台均返回 `ISndSceneHost`） |
| `LevelId` | 关卡唯一标识符 |
| `IsFrontSession` | 指示当前会话是否为前台会话 |
| `GetSessionStateMachines()` | 会话级状态机容器（返回 `IStateMachineContainer`） |

## 设计决策

### 为什么 ISessionRun/ISessionManager 放在 Abstractions 层

这两个接口定义在 Abstractions 层。若放在 Runtime 层，会导致 `ISndSessionAccess`（Abstractions 层）依赖 Runtime 层、违反依赖方向。定义在 Abstractions 层：

- `ISndSessionAccess` 可直接引用同层的 `ISessionManager`/`ISessionRun`
- 策略层通过 `ISndContext` 访问会话能力，不感知 Runtime 层具体实现
- 具体实现（`SessionManager`、`SessionRun`、`EmptySessionManager`）仍留在 Runtime 层

### 为什么 GetSessionStateMachines() 返回 IStateMachineContainer

`ISessionRun` 的 `GetSessionStateMachines()` 返回 `IStateMachineContainer`（Abstractions 层接口），而非具体的 `StateMachineContainer`。这保持接口的抽象层次一致：策略层通过 `ISessionRun` 获取状态机容器时，不依赖 Runtime 层具体类型。

具体实现 `SessionRun` 保留了内部方法返回 `StateMachineContainer`，供 Runtime 层内部代码（如 `ProgressRun`、`SessionManager`）访问 `FlushAllAfterLoad`、`SerializeToNode` 等容器级方法。

### 为什么添加 CanCreateSessions 属性

`EmptySessionManager`（Null Object 模式）在 `ProgressRun` 未建立时作为占位实例。它的 `CreateBackgroundSession` 抛出 `InvalidOperationException`，违反了 Liskov 替换原则：通过 `ISessionManager` 接口使用 `EmptySessionManager` 的消费者必须知道具体类型才能避免异常。添加 `CanCreateSessions` 属性让消费者可以在调用前检查能力，`EmptySessionManager` 安全返回 `false`。

---

[↑ 回到 Abstractions](../README.md)
