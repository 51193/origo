<!-- docsync-pair: Origo.Core/Abstractions/Scene/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Scene (Abstractions)

> [↑ 回到 Abstractions](../README.zh.md) · [↔ 实现: Snd/Scene](../../Snd/Scene/README.zh.md)

## 概述

定义 Core 层编排 SND 场景的抽象能力。四个编排接口（`ISndSceneAccess`、`ISndSceneHost`、`ISndContextAttachableSceneHost`、`IOwningSessionBindable`）均为 `internal`，仅供 Core 会话生命周期与持有 `InternalsVisibleTo` 的适配层实现；业务代码唯一可见的场景接口是只读的 `ISndSceneReadAccess`。策略生命周期钩子由上层会话生命周期（`SndEntityFactory` / `SessionRun`）统一编排。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ISndSceneReadAccess.cs` | public 只读场景访问：`GetEntities` / `FindByName`；状态机钩子与存档元数据贡献者经此查询场景 |
| `ISndSceneAccess.cs` | internal 场景序列化访问：构建元数据列表 / 从元数据恢复（无钩子触发） |
| `ISndSceneHost.cs` | internal 场景宿主（继承 ISndSceneAccess + ISndSceneReadAccess）：实体容器（创建/移除/帧更新） |
| `IOwningSessionBindable.cs` | internal 归属会话绑定：`SetOwningSession(session)`，宿主据此在创建实体时自动绑定 `ISndEntity.OwningSession` |
| `ISndContextAttachableSceneHost.cs` | internal 上下文绑定：`BindContext(context)`，由 `SndContext` / `SessionRun` 启动编排调用 |

## 接口详细

### ISndSceneReadAccess（public）

| 成员 | 说明 |
|------|------|
| `GetEntities()` | 获取当前场景全部存活实体快照 |
| `FindByName(name)` | 按稳定名称查找实体；不存在返回 null |

### ISndSceneAccess（internal）

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

### IOwningSessionBindable

| 成员 | 说明 |
|------|------|
| `SetOwningSession(ISessionRun session)` | 由 `SessionRun` 构造时调用，把会话绑定到宿主；宿主此后创建的每个实体都自动绑定到该会话的 `ISndEntity.OwningSession` |

## 设计决策

### 为什么分离只读访问与编排接口

状态机上下文与存档元数据贡献者只需要查询实体（`GetEntities` / `FindByName`），不需要也不应接触创建、恢复、移除等编排能力。public `ISndSceneReadAccess` 与 internal `ISndSceneAccess` / `ISndSceneHost` 分离后，业务代码无法通过 `GodotSndManager` 强转绕过钩子编排；存档系统与会话管理继续通过 internal 接口访问完整能力。

### 为什么场景宿主不触发策略钩子

场景宿主仅负责实体容器管理，不涉及任何策略生命周期钩子。所有钩子编排由会话生命周期（`SndEntityFactory` / `SessionRun`）统一处理。这种职责分离确保：

- Godot 适配层不参与策略生命周期管理
- 批量操作可以在"全部创建/恢复"和"全部触发钩子"两个阶段之间进行
- 钩子触发期间，所有实体已完全恢复到查找集合中，实现加载顺序无关的跨实体互操作

参见 [IEntityLifecycle](../Entity/README.zh.md) 和 [Scene 实现](../../Snd/Scene/README.zh.md#策略生命周期钩子的编排归属)。

### 为什么 CreateEntity 不做重名校验

`CreateEntity` 保持最小语义，不在接口层做重名校验；框架当前在 spawn 路径上也不强制重名唯一性。接口不承担业务校验职责，将其留给需要时的上层业务规则。

### 为什么 Kill 分为 RequestKillEntity（标记）和 RemoveEntity（拆解）

`RequestKillEntity` 立即标记实体为待销毁（`IsPendingKill = true`），但不立即物理移除。这允许同帧内后续操作通过 `IsPendingKill` 判断实体存活状态，避免延迟 Kill 导致的重复操作。物理销毁在帧末由 `SessionRun.KillPending()`（经 `SessionManager.KillPendingAllSessions()` 对每个会话调用）统一执行（业务队列之后、系统队列之前），先做观察者双向拆线，再批量触发 BeforeDead 钩子、释放策略，最后逐个调用 `RemoveEntity` 拆解。

---

[↑ 回到 Abstractions](../README.zh.md)
