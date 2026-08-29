<!-- docsync-pair: Origo.Core/Abstractions/Entity/README -->
<!-- docsync-revision: 14 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Entity (Abstractions)

> [↑ 回到 Abstractions](../README.zh.md) · [↔ 实现: Snd/Entity](../../Snd/Entity/README.zh.md)

## 概述

定义 SND 实体的抽象接口体系。所有接口遵循接口隔离原则（ISP），将实体的数据、节点、被动策略、主动策略、观察者策略五种能力拆分为独立接口，再由 `ISndEntity` 组合统一入口。`IEntityLifecycle` 为 `internal` 接口，单独定义，供框架与适配层实体共同实现。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ISndDataAccess.cs` | 数据写入、安全读取与强断言读取 |
| `ISndNodeAccess.cs` | 节点查询与枚举 |
| `ISndStrategyAccess.cs` | 被动策略动态添加/移除 |
| `ISndActiveStrategyAccess.cs` | 主动策略：添加/移除/调用 |
| `ISndObserverStrategyAccess.cs` | 观察者策略的挂载/卸载（自观察与跨实体观察） |
| `ISndEntity.cs` | 组合接口：继承上述五个能力接口 + `Name` 属性 + `IsPendingKill` + `OwningSession` 属性 |
| `IEntityLifecycle.cs` | `internal` 框架内部生命周期接口：分阶段恢复/钩子/拆卸方法。实现者：`SndEntity`（Core 内存实体）、适配层实体（桥接委托给内部 `SndEntity`）。适配层与测试项目经 `InternalsVisibleTo` 访问 |

## 接口详细

### ISndDataAccess

| 成员 | 说明 |
|------|------|
| `SetData<T>(name, value)` | 写入命名数据。值与旧值相同则跳过变更通知 |
| `TryGetData<T>(name)` | 安全读取，返回 `(found, value?)` 元组。适用于数据可能不存在或类型不确定的场景 |
| `TryGetData<T>(name, out value)` | 安全读取的 out 参数变体，支持 `if (TryGetData("hp", out var hp))` 惯用法，避免丢弃 found 标志 |
| `GetData<T>(name)` | 强断言读取，数据缺失或类型不匹配时抛出 `InvalidOperationException`。适用于调用方已知数据必定存在的场景（fail-fast） |

> 数据变更的观察不在此接口上。响应数据变更通过 `ObserverStrategyBase` 的 `OnDataChanged` 钩子实现，由 `ISndObserverStrategyAccess` 挂载。详见 [Snd/Strategy](../../Snd/Strategy/README.zh.md)。

### ISndNodeAccess

| 成员 | 说明 |
|------|------|
| `GetNode(name)` | 按名称获取节点句柄 |
| `GetNodeNames()` | 枚举所有已挂载节点名称 |

### ISndStrategyAccess

| 成员 | 说明 |
|------|------|
| `AddStrategy(index)` | 向实体动态添加策略 |
| `RemoveStrategy(index)` | 从实体移除策略 |

### ISndActiveStrategyAccess

| 成员 | 说明 |
|------|------|
| `AddActiveStrategy(index)` | 向实体动态添加主动策略 |
| `RemoveActiveStrategy(index)` | 从实体移除主动策略 |
| `InvokeStrategy(strategyIndex, input?)` | 主动调用策略并返回结果 |

### ISndObserverStrategyAccess

实体观察的统一入口。观察者策略（`ObserverStrategyBase`）通过策略索引挂载到目标实体，框架自动接线目标实体的数据变更并在挂载/卸载时回调 `OnMounted` / `OnUnmounted`。自观察（`targetName == entity.Name`）与跨实体观察使用同一套 API 和同一绑定拓扑格式；绑定关系通过 `StrategyMetaData.ObserverIndices` 持久化，读档时自动恢复。

| 成员 | 说明 |
|------|------|
| `MountObserverStrategy(targetName, observerIndex)` | 按名称挂载观察者策略。`targetName == 自身 Name` 为自观察；跨实体名称解析需场景宿主，应改用 `ISndEntity` 重载 |
| `UnmountObserverStrategy(targetName, observerIndex)` | 按名称卸载，触发 `OnUnmounted` |
| `MountObserverStrategy(target, observerIndex)` | 以已解析的目标实体挂载（跨实体观察首选；目标由 `entity.OwningSession.FindByName(name)` 获得） |
| `UnmountObserverStrategy(target, observerIndex)` | 以目标实体卸载，触发 `OnUnmounted` |

### ISndEntity

`ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess, ISndActiveStrategyAccess, ISndObserverStrategyAccess`

组合后自有的成员：

| 成员 | 说明 |
|------|------|
| `Name { get; }` | 实体唯一标识名；同会话唯一由 spawn/load 编排强制 |
| `OwningSession { get; }` | 实体所属的 `ISessionRun`（非空，fail-fast）：实体创建时由场景宿主自动绑定（`FullMemorySndSceneHost.CreateEntity` / `GodotSndManager.CreateEntity` / `RecoverFromMetaList` 路径）。策略通过此属性直达自己所属的会话（同 session 操作）以及跨 session 通过 `OwningSession.SessionManager` 访问其它会话。未绑定时访问抛 `InvalidOperationException` |
| `IsPendingKill { get; }` | 标记为待销毁状态。框架在帧末统一执行销毁（业务延迟队列之后、系统延迟队列之前）。策略应在操作实体前通过此标志位判断实体是否仍然存活 |

### IEntityLifecycle

**`internal` 框架内部使用的接口**，供 `SndEntityFactory` 和 `SessionRun` 进行两阶段批处理编排，也供适配层实体（如 `GodotSndEntity`）实现以桥接委托。业务代码不应直接调用此接口，也无法在 Core 程序集外引用它（适配层与测试项目经 `InternalsVisibleTo` 访问）。

| 方法 | 阶段 | 说明 |
|------|------|------|
| `RecoverForLifecycle(meta)` | Phase 1 | 恢复 Name + Data + Node + EntityStrategy + ActiveStrategy，不触发任何钩子 |
| `FireAfterSpawnHooks()` | Phase 2 | 触发策略 AfterSpawn |
| `FireAfterLoadHooks()` | Phase 2 | 触发策略 AfterLoad |
| `FireBeforeSaveHooks()` | Phase 2 | 触发策略 BeforeSave |
| `FireBeforeQuitHooks()` | Phase 2 | 触发策略 BeforeQuit |
| `FireBeforeDeadHooks()` | Phase 2 | 触发策略 BeforeDead |
| `ReleaseStrategiesOnly()` | Phase 3 | 释放被动策略 + 主动策略 + 观察者策略引用（不触发钩子） |
| `TeardownOnly()` | Phase 3 | 释放 Node + Data 资源 |
| `TeardownObserverBindings()` | Phase 3 | 经宿主 `ObserverTopology` 卸载本实体的全部观察者绑定：退订目标数据通道、触发 `OnUnmounted` 并归还策略池引用 |
| `BuildMetaData()` | 序列化 | 构建元数据（不触发 BeforeSave） |

实现者：`SndEntity`（Core 内存实体）、适配层实体（如 `GodotSndEntity`，桥接委托给内部 `SndEntity`）。

`ISndEntityRawSubscription`（`Origo.Core/Snd/Entity/`）提供原始的 `TypedData` 级数据订阅接口——`SubscribeDataRaw`、`UnsubscribeDataRaw`。供框架内部的 `ObserverTopology` 将观察者策略接入目标实体的数据变更，不暴露给业务策略代码。

## 设计决策

### 为什么拆分为五个访问接口

五个子接口（`ISndDataAccess`、`ISndNodeAccess`、`ISndStrategyAccess`、`ISndActiveStrategyAccess`、`ISndObserverStrategyAccess`）是 `ISndEntity` 的组合契约，旨在实现清晰的职责划分和内部可测试性。外部代码应直接依赖 `ISndEntity` 统一使用，无需分别引用各子接口。子接口的 ISP 拆分服务于框架内部实现清晰度和测试 mock 的细粒度控制。

### 为什么主动策略独立为 ISndActiveStrategyAccess

主动策略与被动实体策略共享 `BaseStrategy` 和 `SndStrategyPool` 基础设施，但容器完全独立：主动策略使用 Dictionary 实现 O(1) 按索引查找，不参与帧遍历；被动策略使用排序列表，每帧按优先级迭代。接口分离避免消费者耦合到不必要的策略类型。

### 为什么观察统一为观察者策略

数据变更响应与生命周期观察统一收敛到 `ObserverStrategyBase` 一种第一公民策略类型，通过 `ISndObserverStrategyAccess` 以策略索引挂载，而非在实体上暴露委托订阅 API。好处：观察逻辑与策略一样无状态、可池化复用；绑定拓扑可随实体一同序列化（`ObserverIndices`）并在读档时自动恢复，无需业务代码在 `AfterLoad` 中手动重连；接线、拆线和持久化由 `ObserverTopology` 统一治理，消除委托实例匹配、手动退订等易错点。

### 为什么 IEntityLifecycle 单独定义

策略生命周期钩子的触发由框架层控制。将 `RecoverForLifecycle`、`FireXxxHooks`、`ReleaseStrategiesOnly`、`TeardownOnly` 放入独立接口可以：
- 确保 `ISndEntity`（面向业务代码）不暴露生命周期编排能力
- 允许 `SndEntityFactory` 和 `SessionRun` 通过 `IEntityLifecycle` 进行统一批处理
- Godot 适配层的 `GodotSndEntity` 也能实现此接口，委托给内部 `SndEntity`

### 为什么 TryGetData 使用 found/value 元组

参考 Blackboard 的[设计决策](../Blackboard/README.zh.md#设计决策)中的存储理由。

### 为什么观察者钩子签名包含 target 参数

策略实例通过 `SndStrategyPool` 在多个实体间共享，禁止持有实例字段存储实体引用。`OnMounted` / `OnDataChanged` / `OnUnmounted` 的回调同时接收 `entity`（观察者实体）和 `target`（被观察实体），使无状态策略能够在不持有引用的前提下区分二者。自观察时 `entity == target`。

---

[↑ 回到 Abstractions](../README.zh.md)
