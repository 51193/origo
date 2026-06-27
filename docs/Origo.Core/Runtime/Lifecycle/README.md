# Lifecycle

> [↑ 回到 Runtime](../README.md)

## 概述

运行时四层生命周期的实现层。定义了从系统级到会话级的完整启动、运行、退出流程。所有类型通过结构化参数对象构造，上层依赖单向向下传递。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SystemParameters.cs` | 系统层构造参数 |
| `SystemRuntime.cs` | 系统级运行时容器：持有 SystemRun、SystemBlackboard、SndWorld |
| `SystemRun.cs` | 系统层启动：创建 SndWorld → 构造 ProgressRuntime |
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

> `ISessionManager` 和 `ISessionRun` 接口已移至 `Origo.Core.Abstractions.Lifecycle` 命名空间，确保 Abstractions 层不依赖 Runtime 层。本层保留具体实现。

## 四层容器模型

```
SystemRuntime
├── SystemRun
│   └── ProgressRuntime
│       └── ProgressRun
│           └── SessionManagerRuntime (via SessionManager)
│               └── SessionRun (foreground + background)
```

每层容器持有本层的核心对象引用和公共访问入口：
- `SystemRuntime` 持有 `SystemBlackboard`、`SndWorld`、`IScheduler`
- `ProgressRuntime` 持有 `ProgressBlackboard`、`SaveContext`、`SessionManager`
- `SessionManagerRuntime` 持有 `ISessionManager`
- `SessionRun` 持有 `SessionBlackboard`、`ISndSceneHost`、`StateMachineContainer`

## 关键生命周期流程

### 启动

1. `OrigoAutoHost` 创建 `SystemParameters` → `SystemRun.Create()`
2. `SystemRun` 创建 `SndWorld`（策略池 + 配置）→ `ProgressRuntime`
3. 加载初始关卡时：`ProgressRun` 创建 `SessionManager` → `SessionRun`（前台 + 后台）

### 运行

- 每帧：`IScheduler.Tick()` → 执行延迟队列 → `SessionRun.ProcessAllSessions()`
- 控制台命令路由到 `OrigoConsole.ProcessPending()`

### 持久化

- `SaveCoordinator`：独立类（`Origo.Core.Save.SaveCoordinator`），负责构建存档 payload、持久化 progress 状态、管理元数据。从 `ProgressRun` 嵌套类提取为独立类以便测试和职责分离。
- `ProgressRun.RequestSaveGame` → `SaveCoordinator.BuildSavePayload` → `SavePayloadWriter.WriteToCurrent(handle, ...)` → snapshot
- `ProgressRun.RequestLoadGame` → `SavePayloadReader.ReadFromCurrent(handle, ...)` / `ReadFromSnapshot(handle, ...)` → 恢复黑板 + 场景
- `SaveFileHandle`：统一 I/O 上下文（`Origo.Core.Save.Storage.SaveFileHandle`），封装 `IFileMetaAccess` + `IDataSourceIoGateway` + `IPathResolver` + `saveRootPath` + `ISavePathPolicy`。所有 Writer/Reader 方法通过 `SaveFileHandle` 参数接收依赖，消除多参数重载链。
- `PersistProgress`：将流程黑板与完整会话拓扑（前台 + 所有后台）序列化写入 `current/progress.json`。若当前无前台会话则抛出 `InvalidOperationException`，不静默写入部分数据。
- `SessionRun.BuildLevelPayload`：先批量触发 BeforeSave 钩子（`FireBeforeSaveHooks`）在所有实体上，再通过 `SaveContext.BuildSndScene` 构建场景元数据。这确保任何策略在存档前有最后的机会将内存状态刷新到实体 Data 中。
- `SessionRun.LoadFromPayload`：先通过 `SaveContext.RecoverSndScene` 恢复所有实体数据/策略/节点，再批量触发 AfterLoad 钩子（`FireAfterLoadHooks`），最后 Flush 状态机 AfterLoad。这确保所有实体和 ActiveStrategy 已完全恢复后才触发任何策略的 AfterLoad，实现加载顺序无关的跨实体互操作。

### 关卡切换

`SwitchForeground(newLevelId)` 是保存-销毁-加载的组合操作：
1. `PersistForegroundLevelState()` **显式**持久化旧前台关卡数据到 `current/`（调用 `SessionManager.PersistSession`）
2. `PersistAndDestroyBackgroundIfExists(newLevelId)` 若后台会话持有目标 `levelId`，先持久化再销毁
3. `ResetForeground(true)` 销毁当前前台（Dispose 不隐式持久化）
4. `LoadAndMountForeground(newLevelId)` 创建新前台并挂载（`CreateForegroundSession` → 从 `current/` 解析关卡数据）
5. `PersistProgress()` 将新完整拓扑写入 `current/progress.json`

切换完成后，`WriteForegroundTopology` 将新前台与存活的全部后台会话写入流程黑板拓扑；
后续的 `PersistProgress()` 统一将此拓扑与进度状态机落地到磁盘。

### 退出

- `Dispose` 级联：SessionRun → SessionManager → ProgressRun → SystemRun
- `SessionRun.Dispose` 使用两阶段标志：先设 `_disposing`（防重入），执行 BeforeQuit 钩子（此时会话资源仍可访问）和释放策略，再通过 `try/finally` 保证场景集合清空和黑板清除必定执行，最后设 `_disposed`（外部访问正式禁止）
- Dispose 中的清理操作不捕获异常：若 `StateMachines.Clear()`、`ReleaseAllEntities`、`RemoveAllEntities` 或 `Blackboard.Clear()` 抛出异常，异常直接传播到调用方。不再累积 `firstError` 或包装为 `AggregateException`。
- `ProgressRun.Dispose` 中 `SessionManager.Clear()` 和 `DeleteCurrentDirectory()` 的异常同样直接传播，不被静默吞掉。
- 退出前的数据保存应由应用层显式调用 `RequestSaveGame` 完成；`current/` 目录作为临时工作区，在退出时被安全清理

## 设计决策

### 为什么 ProgressRun 使用 partial class 拆分持久化和会话加载

`ProgressRun` 通过 partial class 将持久化逻辑（`SaveCoordinator`）与会话加载逻辑（拓扑编解码、后台会话创建）分离为独立文件，主文件聚焦核心编排流程。`SaveCoordinator` 是独立类 `Origo.Core.Save.SaveCoordinator`（非 `ProgressRun` 的嵌套类），使存档协调逻辑可独立单元测试。

### 为什么 Dispose 不自动持久化

早期设计中 `SessionRun.Dispose` 和 `ProgressRun.Dispose` 会触发 auto-persist，将数据写入 `current/` 后由 `DeleteCurrentDirectory()` 立即删除。这导致：
- 写入 `current/` 随后被删，纯浪费 I/O
- `BeforeSave` 钩子在即将销毁的实体上执行，语义错误且有副作用风险

现在持久化职责完全由调用方显式负责：
- 用户存档：`RequestSaveGame` → `BuildSavePayload` → `WriteSavePayloadToCurrentThenSnapshot`
- 关卡切换：`SwitchForeground` 在销毁旧前台之前**显式**调用 `PersistForegroundLevelState`
- 退出/销毁：只做清理，不做持久化

这确保了每条持久化路径都有明确的语义和可追溯的调用链。

### 为什么读档失败后不回滚 ProgressRun 的内存状态

`ProgressRun.LoadFromPayload` 作用于 `SndContext` 刚创建的全新 `ProgressRun`（先 `CreateProgressRun` 再 `LoadFromPayload`），且磁盘 `current/` 在反序列化之前已写入完整 payload。因此若反序列化或会话挂载中途失败，留下的半反序列化状态属于一个尚未投入使用的 `ProgressRun`，不污染既有运行状态，磁盘数据也保持完整。失败后该 `ProgressRun` 没有前台会话，任何后续 `PersistProgress`/`BuildSavePayload` 都会因缺少前台会话立即抛 `InvalidOperationException`（fail-fast），不会把半状态静默落盘；`LoadFromPayload` 本身幂等，可重新加载覆盖。基于这三点，读档失败路径不额外回滚内存状态。

### 为什么前台会话键固定为 `__foreground__`

前/后台会话共享同一接口 `ISessionRun`，差异仅在于注入的 `ISndSceneHost` 和键名。固定键名消除了"查找前台"的逻辑分支——直接从 SessionManager 中按常量键取值。

### 为什么运行时容器按层分离

每层容器（`SystemRuntime`、`ProgressRuntime` 等）仅暴露本层和下层的能力，上层无法访问下层的实现细节。例如策略只能通过 `ISessionRun` 操作会话，无法访问 `ProgressRun` 内部。

### 为什么 PersistProgress 和 WriteForegroundTopology 写入完整会话拓扑

会话拓扑记录了前台与所有后台会话的键-关卡-同步模式的完整关系。若仅写入前台信息，流程黑板中的拓扑字符串将不包含后台会话，导致 `progress.json` 在切换后丢失后台会话标记。虽然在内存中后台会话仍然存活，但 crash 重启后无法恢复。写入完整拓扑保证了流程黑板始终是当前运行时状态的可恢复快照。

### 为什么 RequestSwitchForegroundLevel 在系统延迟队列中执行

关卡切换是保存-销毁-加载的组合操作，应排在业务逻辑之后、与 Save 操作同队 FIFO 执行。放在系统延迟队列（System Deferred）确保：同帧内的 Save 请求先写入 `current/`，后续的 Switch 的 `LoadAndMountForeground` 从 `current/` 解析时能找到数据。若 Switch 放在业务延迟队列（Business Deferred），Save 尚未执行时 Switch 已尝试加载目标关卡，导致 `current/` 中无数据而回退到空载入。

### 为什么 levelId 必须全局唯一

每个 levelId 对应 `current/level_{id}/` 目录和 `SaveGamePayload.Levels` 中的一个 key。若两个会话同时持有同一 levelId，持久化时后写入者会覆盖前者数据；加载时双方读取同一份已覆盖的 payload。为此 `SessionManager` 在创建会话时校验 levelId 唯一性——若冲突则立即抛出 `InvalidOperationException`。

`SwitchForeground` 在创建新前台前会自动检测后台会话是否持有目标 `levelId`。若冲突，会先调用 `PersistSession` 保存后台数据，再调用 `DestroySession` 销毁该后台，确保 `LoadAndMountForeground` 可以无冲突地创建新前台。调用方无需手动清理冲突的后台会话。

---
[↑ 回到 Runtime](../README.md)
