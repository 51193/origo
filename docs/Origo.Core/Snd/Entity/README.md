# Entity

> [↑ 回到 Snd](../README.md) · [↔ 抽象: Abstractions/Entity](../../Abstractions/Entity/README.md)

## 概述

SND 实体模型的具体实现。`SndEntity` 是运行时实体聚合根，组合了 `SndDataManager`（数据）、`SndNodeManager`（节点）、`SndStrategyManager`（被动策略）、`ActiveStrategyManager`（主动策略）、`ObserverStrategyManager`（观察者策略）五个内部管理器，实现了 `ISndEntity`、`IEntityLifecycle` 和 `ISndEntityRawSubscription` 接口。

策略生命周期钩子通过 `IEntityLifecycle` 接口暴露的分阶段方法触发，由框架层的 `SndRuntime` 和 `SessionRun` 统一编排批量钩子调用，而非由业务代码直接调用实体方法。

`SndEntity` 也是观察系统的宿主：观察者策略（`ObserverStrategyBase`）经 `MountObserverStrategy` 挂载到目标实体，由 `ObserverStrategyManager` 管理绑定拓扑、经 `ISndEntityRawSubscription` 接线目标的数据变更，并在实体退出/死亡时自动卸载。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SndEntity.cs` | 实体聚合根：组合数据/节点/被动策略/主动策略/观察者策略五个管理器，实现 `ISndEntity` + `IEntityLifecycle` + `ISndEntityRawSubscription` |
| `SndDataManager.cs` | `internal` — 实体数据字典管理 + 观察者变更通知 |
| `SndNodeManager.cs` | `internal` — 实体节点管理：从元数据恢复到节点创建/查询/释放 |
| `DataObserverManager.cs` | `internal` — 通用数据观察者订阅/通知基础设施（键 → 回调列表）|
| `ISndEntityRawSubscription.cs` | 原始数据订阅接口（`SubscribeDataRaw` / `UnsubscribeDataRaw`）。供 `ObserverStrategyManager` 在内部链路中直接操作目标实体的 `SndDataManager`，将观察者策略接入数据变更 |

> `TryGetNumericExtensions.cs`（位于 `Origo.Core.Snd` 命名空间）提供 `TryGetNumeric` / `GetNumeric` 扩展方法，桥接 `SetData("k", 5)`（int）和 `TryGetData<float>("k")`（float）之间的类型不匹配。按 float → int → long → double 顺序尝试读取，详见 [TryGetNumeric](../README.md)。

## 模块详解

### SndEntity（聚合根）

**构造函数**要求注入 `INodeFactory`、`SndStrategyPool`、`Func<string, string> sceneAliasResolver`、`ISndContext`、`ILogger`。不暴露无参构造。`sceneAliasResolver` 是场景别名解析函数（从 `SndMappings.ResolveSceneAlias` 提取），避免将整个 `SndMappings` 对象传递到实体层。

**观察者接线**：

观察通过观察者策略实现，挂载入口为 `ISndObserverStrategyAccess` 的四个方法，全部委托给 `ObserverStrategyManager`：

| 公开方法 | 行为 |
|----------|------|
| `MountObserverStrategy(targetName, observerIndex)` | 按名称解析目标（自身 Name 即自观察；跨实体名称需场景宿主），交 `ObserverStrategyManager.Mount` |
| `MountObserverStrategy(target, observerIndex)` | 以已解析目标实体挂载（跨实体首选） |
| `UnmountObserverStrategy(...)` | 对应卸载，触发观察者策略 `OnUnmounted` |

`ObserverStrategyManager` 维护本实体的观察者绑定拓扑，挂载时经目标实体的 `ISndEntityRawSubscription.SubscribeDataRaw` 接入数据变更（由 `[ObserveData]` 声明的键），并将绑定通过 `BuildObserverBindings()` 序列化到 `StrategyMetaData.ObserverIndices`、读档时 `RecoverBindings()` 恢复。

**`IEntityLifecycle` 分阶段方法**：

这些方法供框架层批量编排使用，业务代码不应直接调用：

| 方法 | 阶段 | 说明 |
|------|------|------|
| `RecoverForLifecycle(meta)` | Phase 1: 恢复 | 恢复 Name + Data + Node + EntityStrategy + ActiveStrategy，不触发任何钩子 |
| `FireAfterSpawnHooks()` | Phase 2: 钩子 | 按优先级触发策略 AfterSpawn |
| `FireAfterLoadHooks()` | Phase 2: 钩子 | 按优先级触发策略 AfterLoad |
| `FireBeforeSaveHooks()` | Phase 2: 钩子 | 按优先级触发策略 BeforeSave |
| `FireBeforeQuitHooks()` | Phase 2: 钩子 | 按优先级触发策略 BeforeQuit |
| `FireBeforeDeadHooks()` | Phase 2: 钩子 | 按优先级触发策略 BeforeDead |
| `ReleaseStrategiesOnly()` | Phase 3: 拆卸 | 释放被动策略 + 主动策略 + 观察者策略引用（不触发钩子） |
| `TeardownOnly()` | Phase 3: 拆卸 | 释放 Node + Data 资源 |
| `BuildMetaData()` | 序列化 | 构建元数据（含 ObserverIndices，不触发 BeforeSave） |

`QuitSingle` / `DeadSingle` 的拆卸顺序：先 `FireBeforeQuit/DeadHooks`，再卸载观察者绑定（`OnUnmounted`），然后 `ReleaseStrategiesOnly`，最后 `TeardownOnly`。

`Process(delta)` 按优先级 + 快照迭代触发策略 Process。

`IsPendingKill` 标记由 `RequestKillEntity()` 立即设置。BeforeDead 钩子由 `SndRuntime.KillPendingEntities()` 批量触发，`RemoveEntity()` 仅做拆解。

> **注意**：`CreateEntity` 是场景宿主（`ISndSceneHost`）的方法，不在实体自身上。`ISndSceneHost.CreateEntity` 创建实体并通过 `RecoverForLifecycle` 恢复数据/策略/节点，但不触发 AfterSpawn 钩子。AfterSpawn 钩子由 `SndRuntime.Spawn` / `SndRuntime.SpawnMany` 在创建完成后统一触发。

### SndDataManager

- **存储**：`Dictionary<string, TypedData>`
- **SetData**：用 `CollectionsMarshal.GetValueRefOrAddDefault` 原地写入，旧值相同时跳过通知（避免无意义事件）
- **GetData / GetRequiredData vs TryGetData**：前者 KeyNotFound 或类型不符时抛 `InvalidOperationException`，后者安全返回 `(found, value?)`
- **Subscribe/Unsubscribe**：接收 `Action<ISndEntity, TypedData, TypedData>`（`(target, old, new)`），内部包装为 `Action<TypedData, TypedData>` 适配 `DataObserverManager`；`_subscriptionMap` 存 `(OriginalCallback, WrappedCallback)` 对用于退订匹配。该数据订阅通道经 `ISndEntityRawSubscription` 由 `ObserverStrategyManager` 驱动，不直接暴露给业务策略
- **Recover / Release / SerializeMeta**：存档恢复/清理/序列化

### SndNodeManager

- 实现 `INodeHost`（internal 接口）
- `Recover`：先 Release 旧节点，再按元数据逐个通过 `INodeFactory.Create` 创建新节点。创建失败时回滚 Release 全部
- `Release`：逐个 `node.Free()` 然后清空
- 节点资源 ID 通过 `SndMappings.ResolveSceneAlias` 解析（支持别名）

### DataObserverManager

独立于引擎的观察者基础设施：
- 每个数据键维护一个 `List<Subscription>`
- 每个 Subscription 包含 `Callback(Action<TypedData, TypedData>)` + 可选的 `Filter(Func<TypedData, TypedData, bool>)`
- `NotifyObservers` 通过 `ToArray()` 快照迭代，允许回调中修改订阅列表
- `Unsubscribe` 通过委托引用比对移除

## 设计决策

### 为什么分离 IEntityLifecycle

策略生命周期钩子（AfterSpawn/AfterLoad/BeforeSave/BeforeQuit/BeforeDead）的触发时机由框架层控制，不应该直接暴露在 `ISndEntity`（面向业务代码）上。`IEntityLifecycle` 接口暴露分阶段方法给 `SndRuntime` 和 `SessionRun`，实现批量编排的同时保持业务代码接口简洁。

参见 [IEntityLifecycle](../../Abstractions/Entity/README.md)。

### 为什么 SndEntity 是聚合根而非组合暴露子管理器

外部策略代码通过 `ISndEntity` 接口操作实体（SetData/TryGetData/AddStrategy/MountObserverStrategy），不感知内部管理器。聚合根封装确保实体内部状态一致性。

### 为什么节点恢复失败时回滚全部已创建节点

`SndNodeManager.Recover` 中若第 N 个节点创建失败，前 N-1 个已创建的节点处于半初始状态，无法安全使用。回滚释放确保不残留不完整状态。

### 为什么 DataObserverManager 使用快照迭代通知回调

通知回调中可能触发 Subscribe/Unsubscribe/SetData（从而再次 NotifyObservers）。若直接在列表上 foreach 同时修改，会导致 `Collection was modified` 异常。`ToArray()` 快照以少量分配换取安全。

### 为什么观察经 ObserverStrategyManager 而非实体订阅 API

观察者策略与被动/主动策略一样无状态、可池化。将观察接线交由 `ObserverStrategyManager` 统一治理，使绑定拓扑可随实体序列化（`ObserverIndices`）并在读档时自动恢复，业务代码无需在 `AfterLoad` 中手动重连，也无需在 `BeforeDead` 中手动退订——实体退出/死亡时管理器自动卸载全部绑定。

### 为什么 SndDataManager 存储 (OriginalCallback, WrappedCallback) 对

数据订阅在 `DataObserverManager` 一侧以包装后的 `(old, new)` 委托存在，而退订请求携带的是原始 `(target, old, new)` 委托。`_subscriptionMap` 中的 `SubscriptionPair` 用 `OriginalCallback` 做引用匹配定位订阅，用 `WrappedCallback` 在 `DataObserverManager` 上执行实际退订，保证包装链路可逆。

### 为什么独立 ISndEntityRawSubscription 接口

观察者接线需要直接订阅目标实体的数据变更通道。`ISndEntityRawSubscription` 提供 `TypedData` 级的原始数据订阅入口，成员以显式接口实现暴露——业务代码持有的 `ISndEntity` 看不到这些方法，仅 `ObserverStrategyManager` 等框架内部链路（经 `SndEntity` / `GodotSndEntity`）使用。

---

[↑ 回到 Snd](../README.md)
