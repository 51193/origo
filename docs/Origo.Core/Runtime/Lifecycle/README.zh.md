<!-- docsync-pair: Origo.Core/Runtime/Lifecycle/README -->
<!-- docsync-revision: 19 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Lifecycle

> [↑ 回到 Runtime](../README.zh.md)

## 概述

运行时四层生命周期的实现层。定义了从系统级到会话级的完整启动、运行、退出流程。所有类型通过结构化参数对象构造，上层依赖单向向下传递。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SystemParameters.cs` | 系统层构造参数（含 `AdapterSceneHost`） |
| `SystemRuntime.cs` | 系统级运行时容器：持有 SystemRun、SystemBlackboard、SndWorld |
| `SystemRun.cs` | 系统层启动：持有 OrigoRuntime（其构造时已创建 SndWorld）→ 构造 ProgressRuntime |
| `ProgressParameters.cs` | 流程层构造参数 |
| `ProgressRuntime.cs` | 流程级运行时容器：持有 ProgressRun、ProgressBlackboard、SaveContext |
| `ProgressRun.cs` | 流程层主逻辑：关卡切换、读写存档、会话生命周期编排 |
| `ProgressRun.Persistence.cs` | 流程层持久化委托（通过 SaveCoordinator） |
| `ProgressRun.SessionLoading.cs` | 流程层会话加载分支（partial class）|
| `SessionParameters.cs` | 会话层构造参数 |
| `SessionManager.cs` | 会话管理器完整实现（实现 `ISessionManager`）|
| `SessionManagerRuntime.cs` | 会话管理器运行时容器 |
| `SessionRun.cs` | 会话级别运行时实现（实现 `ISessionRun`）|
| `SessionTopologyCodec.cs` | 会话拓扑编解码（前台+后台会话关系）|
| `SessionStateMachineContext.cs` | internal：会话级状态机上下文适配器，将 SessionBlackboard/SceneAccess 绑定到当前会话 |
| `EmptySessionManager.cs` | 无操作会话管理器（测试/空场景）|
| `RunStateScope.cs` | 运行时状态作用域工具 |
| `TopologyInvariant.cs` | internal — 拓扑不变量校验工具 |

> `ISessionManager` 和 `ISessionRun` 接口定义在 `Origo.Core.Abstractions.Lifecycle` 命名空间，确保 Abstractions 层不依赖 Runtime 层。本层保留具体实现。

## 四层容器模型

```
SystemRun (由 SndContext 构造并持有)
├── SystemRuntime (持有 SndWorld 转发、SystemBlackboard、调度器)
│   └── ProgressRun (由 SndContext 创建)
│       ├── ProgressRuntime (持有 SndWorld、SndContext、StorageService)
│       └── SessionManager (由 ProgressRun 持有)
│           ├── SessionManagerRuntime (持有 SndWorld、SndContext、ProgressBlackboard)
│           └── SessionRun (foreground + background)
```

每层容器持有本层的核心对象引用和公共访问入口：
- `SndContext` 是全局/流程级聚合器：构造 `SystemRun`，并在每次流程生命周期转换（加载/保存/切关卡）时创建/销毁 `ProgressRun`
- `SystemRuntime` 持有 `SndWorld` 转发、`ConverterRegistry`、`AdapterSceneHost` 与调度器（调度器实例实际由 `OrigoRuntime` 持有，经 `IScheduler` 注入）
- `ProgressRuntime` 持有 `Logger`、`StorageService`、`SndWorld`、`AdapterSceneHost`、`StateMachineContext`、`SndContext`、`SavePathPolicy`（进度黑板与状态机容器由 `SessionManager`/`ProgressRun` 分层持有，SaveContext 为瞬态对象按需创建）
- `SessionManagerRuntime` 持有 `SndWorld`、`SndContext`、`ProgressBlackboard` 等运行时依赖（`ISessionManager` 由 `ProgressRun` 持有）
- `SessionManager` 构造时读取 `AdapterSceneHost` 并存储，用于创建前台 session
- `SessionRun` 持有 `SessionBlackboard`、内部 `ISndSceneHost`（`internal`，框架内部使用）、`StateMachineContainer`、实体操作门面

## 关键生命周期流程

### 启动

1. `SndContext` 构造时创建 `SystemRun`（`OrigoRuntime` 构造时已创建 `SndWorld`，经 `SystemParameters` 传入）
2. `SndContext.Bootstrap()`（或 `RequestLoadMainMenuEntrySave`）时创建 `ProgressRun` → `ProgressRun` 创建 `SessionManager` → `SessionRun`（前台 + 后台）

### 运行

- 每帧（`OrigoRuntime.DriveFrame`）：`SessionManager.ProcessAllSessions()` → 业务延迟队列 → `SessionManager.KillPendingAllSessions()` → 系统延迟队列 → 控制台
- 控制台命令由帧驱动路由到 internal `OrigoConsole.ProcessPending()`
- **击杀收割（`KillPending`）的异常语义与 Dispose 对称**：每个 pending 实体按"观察者双向拆线 → `BeforeDead` 钩子 → 策略/节点/数据释放 → 物理移除"四阶段独立处理，阶段间互不阻塞。某个实体的钩子抛异常时，其余实体的清理照常执行、该实体仍被移除（不会永卡 pending），首个失败在收割完成后以原始异常传播（fail-fast），后续失败记 Warning。物理移除（宿主 `RemoveEntity`）失败则立即传播。

### 持久化

- `SaveCoordinator`：独立类（`Origo.Core.Save.SaveCoordinator`），负责构建存档 payload、持久化 progress 状态、管理元数据，便于测试和职责分离。
- `ISndSaveOperations.RequestSaveGame` → `SaveCoordinator.BuildSavePayload` → `SavePayloadWriter.WriteToCurrent(handle, ...)` → snapshot
- `ISndSaveOperations.RequestLoadGame` → `SavePayloadReader.ReadFromCurrent(handle, ...)` / `ReadFromSnapshot(handle, ...)` → 恢复黑板 + 场景
- `SaveFileHandle`：统一 I/O 上下文（`Origo.Core.Save.Storage.SaveFileHandle`），封装 `IFileMetaAccess` + `IDataSourceIoGateway` + `IPathResolver` + `saveRootPath` + `ISavePathPolicy`。所有 Writer/Reader 方法通过 `SaveFileHandle` 参数接收依赖，消除多参数重载链。
- `PersistProgress`：将流程黑板与完整会话拓扑（前台 + 所有后台）序列化写入 `current/progress.json`。若当前无前台会话则抛出 `InvalidOperationException`，不静默写入部分数据。
- `SessionRun.BuildLevelPayload`：先批量触发 BeforeSave 钩子（`FireBeforeSaveHooks`）在所有实体上，再通过 `SaveContext.BuildSndScene` 构建场景元数据。这确保任何策略在存档前有最后的机会将内存状态刷新到实体 Data 中。完整存档路径（`SaveCoordinator.BuildSavePayload`）在序列化前台场景前同样批量触发 `FireBeforeSaveHooks`，与后台会话语义一致。 钩子内覆写框架管理的黑板键（如 `SessionTopology`）会被持久化流程在序列化前以框架计算值覆盖，覆写不生效。BeforeSave 执行期间禁止创建或销毁 Session（`CreateBackgroundSession` / `DestroySession` 抛 `InvalidOperationException`）——会话集合在钩子前已快照，钩子内变更会序列化出不一致的存档。
- `SessionRun.LoadFromPayload`：先通过 `SaveContext.RecoverSndScene` 恢复所有实体数据/策略/节点，再批量触发 AfterLoad 钩子（`FireAfterLoadHooks`），最后 Flush 状态机 AfterLoad。这确保所有实体和 ActiveStrategy 已完全恢复后才触发任何策略的 AfterLoad，实现加载顺序无关的跨实体互操作。AfterLoad 钩子按宿主实体集合快照迭代（钩子内 spawn 的新实体走 spawn 语义、不重复触发 AfterLoad）。观察者绑定随后经宿主拓扑 `RecoverBindingsFor` 恢复；存档的 observer_indices 中引用的实体在恢复场景中缺失时抛 `InvalidOperationException`（fail-fast，严格读取契约）。

### 关卡切换

`SwitchForeground(newLevelId)` 是保存-销毁-加载的组合操作：
1. `PersistForegroundLevelState()` **显式**持久化旧前台关卡数据到 `current/`（调用 `SessionManager.PersistSession`）
2. `PersistAndDestroyBackgroundIfExists(newLevelId)` 若后台会话持有目标 `levelId`，先持久化再销毁
3. `ResetForeground(true)` 销毁当前前台（Dispose 不隐式持久化；**先销毁再清场景**——`SessionRun.Dispose` 依赖宿主实体集合仍在，从而完整执行 BeforeQuit 钩子、观察者拆线与策略池归还，清空宿主后这些步骤会全部空转）
4. `LoadAndMountForeground(newLevelId)` 创建新前台并挂载（`CreateForegroundSession` → 从 `current/` 解析关卡数据）；**若加载失败**，新挂载的半加载前台会被立即 Dispose（清理失败仅记 Warning，不遮蔽原始异常），`ForegroundSession` 回到无前台状态，可安全重试切换
5. `PersistProgress()` 将新完整拓扑写入 `current/progress.json`

切换完成后，`WriteForegroundTopology` 将新前台与存活的全部后台会话写入流程黑板拓扑；
后续的 `PersistProgress()` 统一将此拓扑与进度状态机落地到磁盘。

### 退出

- `Dispose` 级联：SessionRun → SessionManager → ProgressRun → SystemRun
- `SessionRun.Dispose` 使用两阶段标志：先设 `_disposing`（防重入），执行 BeforeQuit 钩子（此时会话资源仍可访问）和释放策略，再通过嵌套 `try/finally` 保证状态机容器清空、实体策略释放、场景集合清空和黑板清除必定执行，最后设 `_disposed`（外部访问正式禁止）。实体释放按宿主集合快照分轮收割（`ReleaseAllEntitiesAndClear`）：钩子内 spawn 的新实体在下一轮被同等释放，已处理实体即时从宿主移除；若四轮内未收敛（钩子不断 spawn）则抛异常显式失败。异常安全：`Disposing` 订阅者或状态机退出 Pop 钩子抛异常时，异常直接传播（fail-fast），但会话状态机（池引用）与实体策略仍保证全部释放、dispose 标志必定提交——与 `ProgressRun.Dispose` 的嵌套 finally 结构对称
- Dispose 中的清理操作不捕获异常：若 `StateMachines.Clear()`、`ReleaseAllEntities`、`RemoveAllEntities` 或 `Blackboard.Clear()` 抛出异常，异常直接传播到调用方，不累积 `firstError` 或包装为 `AggregateException`。
- `ProgressRun.Dispose` 中 `SessionManager.Clear()` 和 `DeleteCurrentDirectory()` 的异常同样直接传播，不被静默吞掉。
- 退出前的数据保存应由应用层显式调用 `RequestSaveGame` 完成；`current/` 目录作为临时工作区，在退出时被安全清理

## 设计决策

### 为什么 ProgressRun 使用 partial class 拆分持久化和会话加载

`ProgressRun` 通过 partial class 将持久化逻辑（`SaveCoordinator`）与会话加载逻辑（拓扑编解码、后台会话创建）分离为独立文件，主文件聚焦核心编排流程。`SaveCoordinator` 是独立类 `Origo.Core.Save.SaveCoordinator`（非 `ProgressRun` 的嵌套类），使存档协调逻辑可独立单元测试。

### 为什么 Dispose 不自动持久化

持久化职责完全由调用方显式负责，`SessionRun.Dispose` 和 `ProgressRun.Dispose` 不触发 auto-persist。若 Dispose 自动写盘：
- 写入 `current/` 随后被 `DeleteCurrentDirectory()` 删除，纯浪费 I/O
- `BeforeSave` 钩子会在即将销毁的实体上执行，语义错误且有副作用风险

因此：
- 用户存档：`RequestSaveGame` → `BuildSavePayload` → `WriteSavePayloadToCurrentThenSnapshot`
- 关卡切换：`SwitchForeground` 在销毁旧前台之前**显式**调用 `PersistForegroundLevelState`
- 退出/销毁：只做清理，不做持久化

这确保了每条持久化路径都有明确的语义和可追溯的调用链。

### 为什么读档失败后弃置 ProgressRun 并清空引用

`ProgressRun.LoadFromPayload` 作用于 `SndContext` 刚创建的全新 `ProgressRun`（先 `CreateProgressRun` 再 `LoadFromPayload`），且磁盘 `current/` 在反序列化之前已写入完整 payload。若反序列化或会话挂载中途失败，`SndContext` 会 **Dispose 该 ProgressRun 并清空上下文引用**（`MountNewProgressRun` 的失败路径）：策略池引用立即归还、`current/` 被清理，且 `ctx.Blackboard.ProgressBlackboard` 与 `ctx.StateMachines` 等读取入口 fail-fast 返回 null/抛出"无活动流程"，不会暴露半反序列化状态。失败异常原样传播（清理失败仅记 Warning 日志，不遮蔽原始异常）。下次流程（如重新 `RequestLoadGame`）从干净状态重新创建 ProgressRun。

回滚的清理步骤同样遵守"不遮蔽原始异常"纪律：`SessionRun.LoadFromPayload` 失败时 `ResetAfterLoadFailure` 逐步执行清理（状态机、实体、场景宿主、黑板），每步独立 try/catch——某一步的用户钩子（如 `OnUnmounted`）抛异常时，后续步骤仍执行，失败汇总为 `AggregateException` 记录 Warning 后，原始加载异常仍原样传播；`ProgressRun` 挂载循环失败时的 `Clear()` 同理（清理失败仅记 Warning）。

### 为什么前台会话键固定为 `__foreground__`

前/后台会话共享同一接口 `ISessionRun`，差异仅在于内部实现（`ISndSceneHost` 的注入方式）和键名。固定键名消除了"查找前台"的逻辑分支——直接从 SessionManager 中按常量键取值。`__foreground__` 是**保留键**：`CreateBackgroundSession` 拒绝使用它（抛 `InvalidOperationException`），前台槽位只能由框架的前台挂载路径（`CreateForegroundSession` / 加载恢复）占据。

### 为什么 ISessionRun 不继承 IDisposable

会话销毁是管理器的能力：业务代码必须通过 `ISessionManager.DestroySession`（或框架的前台切换/清理路径）销毁会话。`ISessionRun` 因此**不暴露** `Dispose()`（`IDisposable` 只由内部具体 `SessionRun` 实现，供框架与测试使用）——若策略可直接 `OwningSession.Dispose()`，销毁就会绕过管理器的挂载校验，形成 §1.4 禁止的第二条访问路径。

### 为什么 DestroySession 是幂等 no-op

销毁不存在的会话不构成契约违反，而是查询式接口的配套清理操作（与 `Contains` / `TryGet` 一致）。框架内部前台切换（`DestroyForeground`）与批量清理（`Clear`）都依赖这一语义，避免在调用方反复做存在性分支。这与“移除未挂载策略/状态机抛异常”不同：后者是修改一个已知聚合实例内部状态，调用方明确持有该对象；前者是按 key 清理管理器容器，调用方可能不知道也不关心槽位是否仍存在。

### 为什么运行时容器按层分离

每层容器（`SystemRuntime`、`ProgressRuntime` 等）仅暴露本层和下层的能力，上层无法访问下层的实现细节。例如策略只能通过 `ISessionRun` 操作会话，无法访问 `ProgressRun` 内部。

### 为什么 PersistProgress 和 WriteForegroundTopology 写入完整会话拓扑

会话拓扑记录了前台与所有后台会话的键-关卡-同步模式的完整关系。若仅写入前台信息，流程黑板中的拓扑字符串将不包含后台会话，导致 `progress.json` 在切换后丢失后台会话标记。虽然在内存中后台会话仍然存活，但 crash 重启后无法恢复。写入完整拓扑保证了流程黑板始终是当前运行时状态的可恢复快照。

### 为什么 RequestSwitchForegroundLevel 在系统延迟队列中执行

关卡切换是保存-销毁-加载的组合操作，应排在业务逻辑之后、与 Save 操作同队 FIFO 执行。放在系统延迟队列（System Deferred）确保：同帧内的 Save 请求先写入 `current/`，后续的 Switch 的 `LoadAndMountForeground` 从 `current/` 解析时能找到数据。若 Switch 放在业务延迟队列（Business Deferred），Save 尚未执行时 Switch 已尝试加载目标关卡，导致 `current/` 中无数据而回退到空载入。

### 为什么 levelId 必须全局唯一

每个 levelId 对应 `current/level_{id}/` 目录和 `SaveGamePayload.Levels` 中的一个 key。若两个会话同时持有同一 levelId，持久化时后写入者会覆盖前者数据；加载时双方读取同一份已覆盖的 payload。为此 `SessionManager` 在创建会话时校验 levelId 唯一性——若冲突则立即抛出 `InvalidOperationException`。

前台槽位的替换不构成并发冲突：`CreateForegroundSession` 先校验目标 levelId 是否被**其他**会话占用，通过后才销毁旧前台，并在此之后构造、挂载新会话。这个顺序保证：与后台会话冲突时当前前台原样保留；旧前台的拆卸钩子执行期间 adapter scene host 仍归属旧会话（新会话尚未构造，不会抢占 `OwningSession` 绑定）。

`SwitchForeground` 在创建新前台前会自动检测后台会话是否持有目标 `levelId`。若冲突，会先调用 `PersistSession` 保存后台数据，再调用 `DestroySession` 销毁该后台，确保 `LoadAndMountForeground` 可以无冲突地创建新前台。调用方无需手动清理冲突的后台会话。

---
[↑ 回到 Runtime](../README.zh.md)
