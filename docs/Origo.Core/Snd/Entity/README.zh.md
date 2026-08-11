<!-- docsync-pair: Origo.Core/Snd/Entity/README -->
<!-- docsync-revision: 12 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Entity

> [↑ 回到 Snd](../README.zh.md) · [↔ 抽象: Abstractions/Entity](../../Abstractions/Entity/README.zh.md)

## 概述

SND 实体模型的具体实现。`SndEntity` 是运行时实体聚合根，组合了 `SndDataManager`（数据）、`SndNodeManager`（节点）、`SndStrategyManager`（被动策略）、`ActiveStrategyManager`（主动策略）四个内部管理器，并持有一个经构造注入的 per-scene-host `ObserverTopology`（观察者绑定）引用，实现了 `ISndEntity`、`IEntityLifecycle` 和 `ISndEntityRawSubscription` 接口。

策略生命周期钩子通过 `IEntityLifecycle` 接口暴露的分阶段方法触发，由框架层的 `SndEntityFactory` 和 `SessionRun` 统一编排批量钩子调用，而非由业务代码直接调用实体方法。

`SndEntity` 也是观察系统的参与者：观察者策略（`ObserverStrategyBase`）经 `MountObserverStrategy` 挂载到目标实体，由 per-scene-host 的 `ObserverTopology` 管理绑定拓扑、经 `ISndEntityRawSubscription` 接线目标的数据变更，并在实体退出/死亡时自动卸载。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SndEntity.cs` | 实体聚合根：组合数据/节点/被动策略/主动策略四个管理器 + 注入的 `ObserverTopology` 引用，实现 `ISndEntity` + `IEntityLifecycle` + `ISndEntityRawSubscription` |
| `SndDataManager.cs` | `internal` — 实体数据字典管理 + 观察者变更通知 |
| `SndNodeManager.cs` | `internal` — 实体节点管理：从元数据恢复到节点创建/查询/释放 |
| `DataObserverManager.cs` | `internal` — 通用数据观察者订阅/通知基础设施（键 → 回调列表）|
| `ISndEntityRawSubscription.cs` | `internal` 原始数据订阅接口（`SubscribeDataRaw` / `UnsubscribeDataRaw`）。供 `ObserverTopology` 在内部链路中直接操作目标实体的 `SndDataManager`，将观察者策略接入数据变更 |

> `TryGetNumericExtensions.cs`（位于 `Origo.Core.Snd` 命名空间）提供 `TryGetNumeric` / `GetNumeric` 扩展方法，桥接 `SetData("k", 5)`（int）和 `TryGetData<float>("k")`（float）之间的类型不匹配。按 float → int → 其余整数类型（byte/sbyte/short/ushort/char/uint/ulong）→ long → double 顺序尝试读取，详见 [TryGetNumeric](../README.zh.md)。

## 模块详解

### SndEntity（聚合根）

**构造函数**要求注入 `INodeFactory`、`SndStrategyPool`、`Func<string, string> sceneAliasResolver`、`ISndContext`、`ILogger`、`ObserverTopology`。不暴露无参构造。`sceneAliasResolver` 是场景别名解析函数（从 `SndMappings.ResolveSceneAlias` 提取），避免将整个 `SndMappings` 对象传递到实体层。`ObserverTopology` 是观察者绑定的核心拓扑，由场景宿主持有并在实体间共享。

**观察者接线**：

观察通过观察者策略实现，挂载入口为 `ISndObserverStrategyAccess` 的四个方法，全部委托给注入的 per-scene-host `ObserverTopology`：

| 公开方法 | 行为 |
|----------|------|
| `MountObserverStrategy(targetName, observerIndex)` | 按名称解析目标（自身 Name 即自观察；跨实体名称需场景宿主），交 `ObserverTopology.Mount` |
| `MountObserverStrategy(target, observerIndex)` | 以已解析目标实体挂载（跨实体首选） |
| `UnmountObserverStrategy(...)` | 对应卸载，触发观察者策略 `OnUnmounted` |

`ObserverTopology` 维护本宿主内所有实体的观察者绑定拓扑，挂载时经目标实体的 `ISndEntityRawSubscription.SubscribeDataRaw` 接入数据变更（由 `[ObserveData]` 声明的键），并将本实体的出边通过 `BuildBindingsFor(Name)` 序列化到 `StrategyMetaData.ObserverIndices`、读档时由 `SessionRun` 经宿主拓扑 `RecoverBindingsFor()` 恢复。

**`IEntityLifecycle` 分阶段方法**（`internal` 接口）：

这些方法供框架层批量编排使用，业务代码不应直接调用：

| 方法 | 阶段 | 说明 |
|------|------|------|
| `RecoverForLifecycle(meta)` | Phase 1: 恢复 | 恢复 Name + Data + Node + EntityStrategy + ActiveStrategy，不触发任何钩子。失败时跨阶段原子回滚：已获取的策略引用归还池、已创建的节点释放后异常再传播 |
| `FireAfterSpawnHooks()` | Phase 2: 钩子 | 按优先级触发策略 AfterSpawn |
| `FireAfterLoadHooks()` | Phase 2: 钩子 | 按优先级触发策略 AfterLoad |
| `FireBeforeSaveHooks()` | Phase 2: 钩子 | 按优先级触发策略 BeforeSave |
| `FireBeforeQuitHooks()` | Phase 2: 钩子 | 按优先级触发策略 BeforeQuit |
| `FireBeforeDeadHooks()` | Phase 2: 钩子 | 按优先级触发策略 BeforeDead |
| `ReleaseStrategiesOnly()` | Phase 3: 拆卸 | 释放被动策略 + 主动策略 + 观察者策略引用（不触发钩子） |
| `TeardownOnly()` | Phase 3: 拆卸 | 释放 Node + Data 资源 |
| `TeardownObserverBindings()` | Phase 3: 拆卸 | 经宿主 `ObserverTopology` 卸载本实体全部观察者绑定（退订目标数据通道） |
| `BuildMetaData()` | 序列化 | 构建元数据（含 ObserverIndices，不触发 BeforeSave） |

> **可见性**：`IEntityLifecycle` 与 `SndEntity.Process` 均为 `internal`——实体生命周期编排只能经 `ISessionRun`（`Spawn` / `SpawnMany` / `RequestKillEntity`）与框架内部的批量钩子管线触发。适配层与测试项目经 `InternalsVisibleTo` 访问。spawn/load/save 统一经 `SndEntityFactory` / `SessionRun` / 序列化管线的批量路径，不提供单实体便捷方法。

会话退出（quit）的拆卸顺序（`SessionRun.ReleaseAllEntitiesAndClear`）：先批量触发 `FireBeforeQuitHooks`，再卸载全部观察者绑定（`TeardownObserverBindings`，触发 `OnUnmounted` 并退订目标数据通道），然后批量 `ReleaseStrategiesOnly`，最后 `TeardownOnly`。实体销毁（kill）路径（`SessionRun.KillPending`）先做观察者双向拆线，再触发 `FireBeforeDeadHooks`，最后释放并移除。

`Process(delta)` 按优先级 + 快照迭代触发策略 Process（`internal`，由场景宿主 `ProcessAll` 与适配层帧处理调用）。

`IsPendingKill` 标记由 `RequestKillEntity()` 立即设置。BeforeDead 钩子由 `SessionRun.KillPending()` 批量触发，`RemoveEntity()` 仅做拆解。

> **注意**：`CreateEntity` 是场景宿主（`ISndSceneHost`）的方法，不在实体自身上。`ISndSceneHost.CreateEntity` 创建实体并通过 `RecoverForLifecycle` 恢复数据/策略/节点，但不触发 AfterSpawn 钩子。AfterSpawn 钩子由 `SndEntityFactory.Spawn` / `SndEntityFactory.SpawnMany` 在创建完成后统一触发。

### SndDataManager

- **存储**：`Dictionary<string, TypedData>`
- **SetData**：用 `CollectionsMarshal.GetValueRefOrAddDefault` 原地写入，旧值相同时跳过通知（避免无意义事件）。引用类型值为 null 时抛出 `ArgumentNullException`。
- **通知契约**：值**先提交**再通知观察者——回调抛异常不会回滚数据；观察者按订阅顺序依次通知，某个回调抛异常时中止同键的剩余通知并向上传播（fail-fast）。
- **GetData / GetRequiredData vs TryGetData**：`GetData` 和 `GetRequiredData` 要求 `T : notnull`。前者 KeyNotFound 或类型不符时抛 `InvalidOperationException`，后者安全返回 `(found, value?)`
- **Subscribe/Unsubscribe**：接收 `Action<ISndEntity, TypedData, TypedData>`（`(target, old, new)`），内部包装为 `Action<TypedData, TypedData>` 适配 `DataObserverManager`；`_subscriptionMap` 存 `(OriginalCallback, WrappedCallback)` 对用于退订匹配。该数据订阅通道经 `ISndEntityRawSubscription` 由 `ObserverTopology` 驱动，不直接暴露给业务策略
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

策略生命周期钩子（AfterSpawn/AfterLoad/BeforeSave/BeforeQuit/BeforeDead）的触发时机由框架层控制，不应该直接暴露在 `ISndEntity`（面向业务代码）上。`IEntityLifecycle` 接口暴露分阶段方法给 `SndEntityFactory` 和 `SessionRun`，实现批量编排的同时保持业务代码接口简洁。

参见 [IEntityLifecycle](../../Abstractions/Entity/README.zh.md)。

### 为什么 SndEntity 是聚合根而非组合暴露子管理器

外部策略代码通过 `ISndEntity` 接口操作实体（SetData/TryGetData/AddStrategy/MountObserverStrategy），不感知内部管理器。聚合根封装确保实体内部状态一致性。

### 为什么节点恢复失败时回滚全部已创建节点

`SndNodeManager.Recover` 中若第 N 个节点创建失败，前 N-1 个已创建的节点处于半初始状态，无法安全使用。回滚释放确保不残留不完整状态。

### 为什么 DataObserverManager 使用快照迭代通知回调

通知回调中可能触发 Subscribe/Unsubscribe/SetData（从而再次 NotifyObservers）。若直接在列表上 foreach 同时修改，会导致 `Collection was modified` 异常。`ToArray()` 快照以少量分配换取安全。

### 为什么观察经 per-scene-host ObserverTopology 而非实体订阅 API

观察者策略与被动/主动策略一样无状态、可池化。将观察接线交由场景宿主级的 `ObserverTopology` 统一治理，使绑定拓扑可随实体序列化（`ObserverIndices`）并在读档时自动恢复，业务代码无需在 `AfterLoad` 中手动重连，也无需在 `BeforeDead` 中手动退订——实体退出/死亡时拓扑自动卸载全部绑定。跨实体绑定是有向图而非每实体私有状态，集中到 per-scene-host 拓扑后，`SessionRun` 的 kill/clear 双向 teardown 经入边索引定位观察者，无需实体反向暴露内部管理器。

> **⚠️ 适配层契约**：`SessionRun.KillPending` 的 observer 双向 teardown 与读档恢复对宿主内**所有实体类型**（裸 `SndEntity` 与适配层包装实体，如 Godot 前台的 `GodotSndEntity`）统一生效——拆线经宿主拓扑按实体名称与 `ISndEntityRawSubscription` 接口完成，不依赖具体实体类型，`OnUnmounted` 在 `BeforeDead` 钩子之前触发。适配层无需为包装实体另行实现观察者拆线（见 [Scene/README](../Scene/README.zh.md)）。

### 为什么 SndDataManager 存储 (OriginalCallback, WrappedCallback) 对

数据订阅在 `DataObserverManager` 一侧以包装后的 `(old, new)` 委托存在，而退订请求携带的是原始 `(target, old, new)` 委托。`_subscriptionMap` 中的 `SubscriptionPair` 用 `OriginalCallback` 做引用匹配定位订阅，用 `WrappedCallback` 在 `DataObserverManager` 上执行实际退订，保证包装链路可逆。

### 为什么独立 ISndEntityRawSubscription 接口

观察者接线需要直接订阅目标实体的数据变更通道。`ISndEntityRawSubscription` 提供 `TypedData` 级的原始数据订阅入口，成员以显式接口实现暴露——业务代码持有的 `ISndEntity` 看不到这些方法，仅 `ObserverTopology` 等框架内部链路（经 `SndEntity` / `GodotSndEntity`）使用。

---

[↑ 回到 Snd](../README.zh.md)
