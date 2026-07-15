<!-- docsync-pair: Origo.Core.Tests/Runtime-Core -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 运行时核心 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Runtime](../Origo.Core/Runtime/README.zh.md)
> [↔ 被测行为: usage/architecture-overview](../usage/architecture-overview.zh.md)

## 被测行为概览

验证 OrigoRuntime 的基础构造和控制台注入、帧末延迟动作队列的刷新、实体 Kill/KillAll 的标记—收割两阶段语义，以及 ActionScheduler 的入队/嵌套执行/清空。

`SchedulingAndTypeMappingTests.cs` 同时承载 ActionScheduler 与 TypeStringMapping 两种能力的测试：本文档记录其 ActionScheduler 相关方法；其 TypeStringMapping 方法（`TypeStringMapping_HasDefaultTypes_AndSupportsCustomRegistration`）属于类型序列化能力，记录于 [TypeStringMapping.md](TypeStringMapping.zh.md)，本文档不重复收录。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `OrigoRuntimeBasicTests.cs` | OrigoRuntime 构造、SndWorld 创建、控制台注入/未注入、ResetConsoleState、FlushEndOfFrameDeferred、IOrigoFrameDriver.DriveFrame |
| `EntityKillTests.cs` | 实体 Kill/KillAll：标记为待销毁（IsPendingKill）、KillPendingAllSessions 收割、BeforeDead/BeforeQuit 钩子、kill_all 命令 |
| `SchedulingAndTypeMappingTests.cs` | ActionScheduler 的入队/嵌套执行/清空（TypeStringMapping 方法见 [TypeStringMapping.md](TypeStringMapping.zh.md)） |

## OrigoRuntimeBasicTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `OrigoRuntime_Constructor_CreatesSndWorld` | 构造后 SndWorld 和 Logger 可用 | Runtime: OrigoRuntime |
| `OrigoRuntime_ConsoleInputBuffer_NullWithoutInjection` | 未注入控制台时 Console 相关属性为 null | Runtime: Console |
| `OrigoRuntime_WithConsole_CreatesConsole` | 注入控制台输入/输出后 Console 可用 | Runtime: Console |
| `OrigoRuntime_ResetConsoleState_ClearsInputQueue` | 重置只清理输入队列 | Runtime: Console |
| `OrigoRuntime_FlushEndOfFrameDeferred_ExecutesDeferredActions` | Business 和 System 延迟动作全部执行 | Scheduling |
| `OrigoRuntime_DriveFrame_DelegatesToFlushAndConsole` | IOrigoFrameDriver.DriveFrame(delta) 执行业务延迟队列并处理控制台 pending | Abstractions/Runtime: IOrigoFrameDriver |

## EntityKillTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RequestKillEntity_TriggersBeforeDead_ViaFlush` | RequestKillEntity 先标记 IsPendingKill，收割时触发 BeforeDead 并移除 | Runtime: 实体生命周期 |
| `ManualIterateAndRequestKillEntity_MarksAllAliveEntities` | 遍历会话实体逐一 RequestKillEntity，所有存活实体被标记 | Runtime: SessionManager |
| `RequestKillAll_SkipsAlreadyPendingEntities` | 遍历标记时跳过已 IsPendingKill 的实体，不重复请求 | Runtime: SessionManager |
| `RequestKillAll_RemovesAllAfterFlush` | 标记全部后 KillPendingAllSessions 移除全部实体 | Runtime: SessionManager |
| `KillPendingEntities_FiresBeforeDead` | 收割触发 BeforeDead 钩子并移除实体 | Runtime: 实体生命周期 |
| `KillPendingEntities_BusinessDeferredBeforeKillSweep` | 业务延迟动作按入队顺序执行，且收割在其后触发 BeforeDead | Scheduling |
| `KillPendingAllSessions_RemovesPendingEntities` | KillPendingAllSessions 移除被标记的实体 | Runtime: SessionManager |
| `DeadByName_RemovesEntity` | RemoveEntity 后实体从场景移除 | Runtime: 实体生命周期 |
| `StubSndSceneHost_DeadByName_RemovesEntity` | Stub 宿主 RemoveEntity 移除实体 | Runtime: ISndSceneHost |
| `StubSndSceneHost_RequestKillEntity_MarksPendingKill` | Stub 宿主 RequestKillEntity 标记 IsPendingKill，实体仍在集合 | Runtime: ISndSceneHost |
| `IsPendingKill_CanBeCheckedByStrategy` | RequestKillEntity 后策略可读取 IsPendingKill | Runtime: 实体生命周期 |
| `ClearAll_TriggersBeforeQuit` | FireBeforeQuitHooks + RemoveAllEntities 触发 BeforeQuit 并清空场景 | Runtime: 实体生命周期 |
| `KillAllCommand_MarksAllEntities` | kill_all 命令标记全部实体为 IsPendingKill | console-commands: kill_all |
| `KillAllCommand_SkipsAlreadyPending` | kill_all 命令对已标记实体保持 IsPendingKill | console-commands: kill_all |
| `FullCycle_ProcessMarksThenFlushRemoves` | ProcessAll → RequestKillEntity → KillPendingAllSessions 全周期移除实体 | Runtime: SessionManager |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `StubSndSceneHost_RequestKillEntity_Missing_Throws` | RequestKillEntity 不存在的实体 | InvalidOperationException |
| `StubSndSceneHost_RequestKillEntity_AlreadyPending_Throws` | 对已 IsPendingKill 的实体重复 RequestKillEntity | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `RequestKillAll_EmptyScene_DoesNotThrow` | 空场景遍历并 RequestKillEntity | 不抛异常 |
| `KillPendingEntities_NoPendingEntities_DoesNotThrow` | 无待销毁实体时 KillPendingAllSessions | 不抛异常，实体保留 |
| `StubSndSceneHost_DeadByName_MissingEntity_NoError` | RemoveEntity 不存在的实体 | 不抛异常 |
| `IsPendingKill_DefaultFalse` | 新建实体 | IsPendingKill 默认为 false |

## SchedulingAndTypeMappingTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ActionScheduler_Tick_ExecutesQueuedAndNestedActions` | Tick 执行已入队及执行中再入队的嵌套 action，返回执行计数与正确顺序 | Scheduling |
| `ActionScheduler_Clear_RemovesPendingActions` | Clear 后 Tick 不执行已清空的 action，返回 0 | Scheduling |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `KillProbeStrategy` | EntityKillTests.cs | LifecycleStrategyBase 探针：BeforeDead 时记录 "before_dead" 事件，验证 Kill 收割触发钩子 |
| `QuitProbeStrategy` | EntityKillTests.cs | LifecycleStrategyBase 探针：BeforeQuit 时记录 "before_quit" 事件，验证 ClearAll 触发退出钩子 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| OrigoRuntime 在构造后立即 Dispose 的行为 | 资源释放的正确性 | Runtime |
| SystemBlackboard 在多个 ProgressRun 间的隔离 | 系统黑板是否跨 Progress 共享 | Runtime: 四层运行时 |
| ActionScheduler 嵌套深度的递归保护边界 | 无限再入队时的深度上限语义 | Scheduling |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
