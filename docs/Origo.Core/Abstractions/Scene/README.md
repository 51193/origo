# Scene (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: Snd/Scene](../../Snd/Scene/README.md)

## 概述

定义 Core 层编排 SND 场景的抽象能力。`ISndSceneAccess` 提供最小化的构建/恢复操作（纯数据转换，不触发策略钩子），`ISndSceneHost` 在其基础上补充实体容器管理能力。策略生命周期钩子由上层 `SndRuntime` 和 `SessionRun` 统一编排。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ISndSceneAccess.cs` | 最小场景访问：构建元数据列表 / 从元数据恢复（无钩子触发） |
| `ISndSceneHost.cs` | 场景宿主（继承 ISndSceneAccess）：实体容器（创建/查找/移除/帧更新） |

## 接口详细

### ISndSceneAccess

| 成员 | 说明 |
|------|------|
| `BuildMetaList()` | 收集当前场景全部实体元数据列表（不触发 BeforeSave 钩子） |
| `RecoverFromMetaList(metaList)` | 从元数据列表恢复实体数据/策略/节点（不触发 AfterLoad 钩子） |

### ISndSceneHost : ISndSceneAccess

| 自有成员 | 说明 |
|------|------|
| `CreateEntity(SndMetaData)` | 创建实体 + 恢复数据/策略/节点，不触发任何策略生命周期钩子（不校验重名） |
| `GetEntities()` | 枚举所有存活实体 |
| `FindByName(name)` | 按名查找实体 |
| `ProcessAll(delta)` | 对所有存活实体执行帧更新 |
| `RequestKillEntity(name)` | 立即将指定实体标记为待销毁（帧末统一执行）。若实体不存在或已标记则抛异常 |
| `RemoveEntity(name)` | 从集合移除 + 释放引擎资源（节点/数据），不释放策略、不触发钩子。BeforeDead 钩子和策略释放由框架在调用前统一完成 |
| `RemoveAllEntities()` | 清空场景实体集合引用（不触发钩子、不释放策略）。BeforeQuit 钩子和策略释放由框架在调用前统一完成 |

## 设计决策

### 为什么分离 ISndSceneAccess 和 ISndSceneHost

状态机上下文只需要 `ISndSceneAccess` 的构建/恢复能力，不需要实体容器管理。ISP 分离让依赖更精确：存档系统通过 `ISndSceneAccess` 操作场景，会话管理通过 `ISndSceneHost` 管理实体集合。两者对外暴露不同消费场景。

### 为什么场景宿主不触发策略钩子

场景宿主仅负责实体容器管理，不涉及任何策略生命周期钩子。所有钩子编排由 `SndRuntime` 统一处理。这种职责分离确保：

- Godot 适配层不参与策略生命周期管理
- 批量操作可以在"全部创建/恢复"和"全部触发钩子"两个阶段之间进行
- 钩子触发期间，所有实体已完全恢复到查找集合中，实现加载顺序无关的跨实体互操作

参见 [IEntityLifecycle](../../Abstractions/Entity/README.md) 和 [SndRuntime](../../Snd/Scene/README.md#sndruntime-生命周期编排)。

### 为什么 CreateEntity 不做重名校验

重名校验是上层业务规则（通过 `SndRuntime.Spawn` 执行），不在接口层强制。接口保持最小语义，将校验职责留给编排层。

### 为什么 Kill 分为 RequestKillEntity（标记）和 RemoveEntity（拆解）

`RequestKillEntity` 立即标记实体为待销毁（`IsPendingKill = true`），但不立即物理移除。这允许同帧内后续操作通过 `IsPendingKill` 判断实体存活状态，避免延迟 Kill 导致的重复操作。物理销毁在帧末由 `KillPendingEntities()` 统一执行（业务队列之后、系统队列之前），先批量触发 BeforeDead 钩子、释放策略，再逐个调用 `RemoveEntity` 拆解。

---

[↑ 回到 Abstractions](../README.md)
