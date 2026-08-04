<!-- docsync-pair: Origo.Core/Snd/Scene/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Scene

> [↑ 回到 Snd](../README.zh.md)

## 概述

SND 场景宿主实现层。提供 `ISndSceneHost` 的两种实现：完整内存宿主（用于后台会话）、轻量存根宿主（用于测试和设备无关的离线构建）。场景宿主仅负责实体容器管理（创建/查找/移除/帧更新），**不触发任何策略生命周期钩子**。钩子编排归属上层会话生命周期：`SndEntityFactory` 负责 spawn 后的 AfterSpawn，`SessionRun` 负责 load/save/quit/kill 阶段的批量钩子，`SessionManager` 驱动多会话的帧更新与帧末收割。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SndEntityFactory.cs` | 公共静态工具：`Spawn(host, meta)` = `host.CreateEntity` + 触发 AfterSpawn；`SpawnMany(host, metas)` = 两阶段（全部创建后再统一触发 AfterSpawn） |
| `FullMemorySndSceneHost.cs` | 完整内存场景宿主，创建真实 SndEntity，持有 per-scene-host 观察者拓扑，支持归属会话绑定 |
| `StubSndSceneHost.cs` | 轻量存根场景宿主，使用简单 StubSndEntity（无策略/节点），用于单元测试和 LevelBuilder 离线构建 |
| `ISndContextAttachableSceneHost.cs` | 接口：允许会话构造时把 `ISndContext` 绑定到宿主（`BindContext`） |
| `IObserverTopologyHost.cs` | `internal` 接口：暴露宿主持有的 per-scene-host 观察者拓扑（`ObserverTopology`），供 `SessionRun`/`SessionManager` 编排跨实体观察者绑定的拆线与读档恢复 |
| `NullNodeFactory.cs` | 内存级节点工厂，创建无操作句柄 |

> 归属会话绑定接口 [`IOwningSessionBindable`](../../Abstractions/Scene/README.zh.md) 定义在 Abstractions/Scene 层，由 `FullMemorySndSceneHost`（及适配层 `GodotSndManager`）实现。

## 模块详解

### 策略生命周期钩子的编排归属

场景宿主只创建/恢复/容纳实体，钩子由调用方在恰当阶段统一批量触发：

| 阶段 | 编排者 | 说明 |
|------|--------|------|
| AfterSpawn | `SndEntityFactory.Spawn` / `SpawnMany` | 创建（批量则全部创建）后统一 `FireAfterSpawnHooks()` |
| AfterLoad + 观察者绑定恢复 | `SessionRun.LoadFromPayload` | 先 `FireAfterLoadHooks()`，再从 `ObserverIndices` 经宿主拓扑 `RecoverBindingsFor` 接线 |
| BeforeSave | `SessionRun`（`BuildLevelPayload`） | 序列化前对全部实体 `FireBeforeSaveHooks()` |
| 观察者双向拆线 + BeforeDead + 物理移除 | `SessionRun.KillPending` | 由 `SessionManager.KillPendingAllSessions` 在帧末对每个会话调用 |
| BeforeQuit + 释放 + 清空 | `SessionRun.Dispose` | `FireBeforeQuitHooks()` → `ReleaseStrategiesOnly` + `TeardownOnly` → `SceneHost.RemoveAllEntities()` |
| 帧更新 | `SessionManager.ProcessAllSessions` | 对参与 Process 的会话透传 `SceneHost.ProcessAll(delta)` |

`SceneHost.CreateEntity` 仅负责创建和恢复（`RecoverForLifecycle`），不触发任何钩子。

### FullMemorySndSceneHost

后台会话的默认场景宿主。关键特性：
- 通过 `SndWorld.CreateEntity` 创建完整 `SndEntity`（非简单内存实体）
- 实现 `IObserverTopologyHost`：在 `BindWorld` 时创建 per-scene-host `ObserverTopology`，并将其注入它创建的每个实体；该宿主内所有实体的观察者绑定集中于此拓扑
- 实现 `IOwningSessionBindable`：`SessionRun` 构造时经 `SetOwningSession` 绑定归属会话；此后 `CreateEntity` 创建的每个实体在 `RecoverForLifecycle` 之后经 `entity.BindSession` 自动绑定到该会话的 `OwningSession`
- 实现 `ISndContextAttachableSceneHost`：会话构造时经 `BindContext` 注入 `ISndContext`
- 需延迟绑定 `SndWorld` 和 `ISndContext`（配合 `OrigoRuntime` 两阶段构造）
- **实体容器管理**：只负责创建、查找、移除实体，不触发任何策略钩子
- **CreateEntity**：创建实体，恢复数据/策略/节点（通过 `RecoverForLifecycle`），绑定归属会话，不触发 AfterSpawn 钩子
- **RecoverFromMetaList**：仅恢复实体数据/策略/节点（不触发 AfterLoad 钩子），用于存档加载场景。先通过 `entity.Name = metaData.Name` 设置实体名，再将实体注册到内部集合，最后调用 `RecoverForLifecycle(meta)`。因此钩子执行前，`FindByName` 可查找所有已注册实体。
- **RemoveEntity**：仅从集合移除实体，不释放策略引用、不释放引擎资源、不触发钩子（策略释放与资源回收由 `SessionRun.KillPending` 在调用前完成）
- **RemoveAllEntities**：仅清空内部集合
- **ProcessAll**：按索引循环迭代所有存活实体（迭代期间宿主容器不应被修改）

### StubSndSceneHost

轻量实现，直接使用内嵌的 `StubSndEntity` 类。这个实体不支持节点访问、策略和订阅，仅支持基础键值数据存取。用于单元测试和 `LevelBuilder` 离线构建。

> `StubSndSceneHost` 的命名表达其"存根"语义——无策略/无节点的轻量占位实现，非完整内存宿主。

### NullNodeFactory / NullNodeHandle

用于 `FullMemorySndSceneHost`。`Create()` 返回不绑定任何引擎节点的句柄，所有操作（`Free`、`SetVisible`）为空操作。Core 层后台会话不需要实际渲染节点。

## 设计决策

### 为什么场景宿主不触发策略钩子

所有策略生命周期钩子的触发由会话生命周期（`SndEntityFactory` / `SessionRun` / `SessionManager`）统一编排。场景宿主仅负责实体容器管理。这种职责分离确保：

- Godot 适配层（`GodotSndManager`）不参与策略生命周期管理
- 批量操作可以在"全部创建/恢复"阶段和"全部触发钩子"阶段之间进行
- 钩子触发期间，所有实体已完全恢复并注册到查找集合，实现加载顺序无关的跨实体互操作

### 为什么需要两个场景宿主

`FullMemorySndSceneHost` 提供完整策略生命周期但需要 `SndWorld` 和 `ISndContext` 的上游依赖；`StubSndSceneHost` 零依赖、完全自治但不能运行策略。前者用于后台会话，后者用于测试和离线构建（测试中通常只测数据流转而无需策略执行）。

### 为什么 spawn 逻辑集中在 SndEntityFactory

`SndEntityFactory.Spawn/SpawnMany` 是"创建实体 + 触发 AfterSpawn"的唯一权威实现。`ISessionRun.Spawn/SpawnMany` 委托给它，适配层与自动初始化器也复用它。单一来源保证调整 spawn 行为只需改一处，避免多套 spawn 逻辑产生分歧。`SndEntityFactory.SpawnMany` 采用两阶段（全部创建后再统一触发钩子），使 AfterSpawn 钩子可见全部兄弟实体。

### 为什么 FullMemorySndSceneHost 延迟绑定 World/Context

`OrigoRuntime` 的两阶段构造（先创建宿主，后注入运行时依赖）需要宿主支持延迟绑定。在构造函数中提供这些依赖会形成循环：`SndWorld` 的创建需要 `OrigoRuntime`，而宿主的创建又在 `SndWorld` 之前。

### 为什么实体在钩子触发前先登记到查找集合

策略钩子可能需要在创建期间引用兄弟实体（例如通过 `FindByName` 查找依赖实体、挂载跨实体观察者绑定）。先登记后触发钩子保证了所有实体在整个生命周期内始终可被检索。批处理模式下，所有实体先全部登记，再统一触发钩子，进一步加强了这一保证。

### 为什么观察者拓扑按场景宿主划分

观察者绑定是 session 内的有向图（target 解析始终在单一宿主的 `FindByName` 范围内）。每个创建真实 `SndEntity` 的宿主（`FullMemorySndSceneHost`、`GodotSndManager`）持有一个 `ObserverTopology` 并实现 `IObserverTopologyHost`，拓扑与宿主同生命周期。`SessionRun` 经该接口获取宿主拓扑，对其中 `SndEntity` 类型的实体编排 kill/clear 双向 teardown 与读档恢复；宿主内非裸 `SndEntity` 的包装实体类型（如 Godot 前台实体）按约定不参与 `SessionRun.KillPending` 的 observer 双向 teardown。`StubSndSceneHost` 不创建真实实体，不实现该接口。集中到宿主级拓扑后，实体无需反向暴露内部观察者管理器即可完成跨实体的接线、拆线与恢复。

### 为什么实体在创建期绑定归属会话

策略钩子经 `entity.OwningSession` 获知自身所属会话（而非反查全局上下文）。归属在实体**创建期**即确定：`SessionRun` 构造时经 `IOwningSessionBindable.SetOwningSession` 把自身绑定到宿主，此后宿主 `CreateEntity` 创建的每个实体都在 `RecoverForLifecycle` 之后经 `entity.BindSession` 绑定到该会话。

这样无论实体经 `SessionManager` 编排路径创建，还是被**直接 spawn 到某后台会话的宿主**（如在后台预构建世界后再切前台），其 `OwningSession` 始终指向真正拥有它的会话，钩子归属不会误判。`StubSndSceneHost` 不创建真实实体，不实现该接口。

---

[↑ 回到 Snd](../README.zh.md)
