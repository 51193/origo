# 帧驱动游戏模拟集成测试

> [↑ 回到 Origo.Core.Tests](../README.md)
> [↔ 被测行为: usage/architecture-overview](../../usage/architecture-overview.md)
> [↔ 被测模块: Origo.Core/Runtime](../../Origo.Core/Runtime/README.md)

## 被测行为概览

验证 `IOrigoFrameDriver.DriveFrame(delta)` 驱动的完整帧模拟管线：
从 `OrigoRuntime` → `SndContext` → `ProgressRun` → `SessionManager` → `SessionRun`
四层运行时在真实 `SndEntity` 实体与策略的参与下，按帧循环顺序执行实体处理、
业务延迟队列、实体收割和系统延迟队列。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GameplayIntegrationTests.cs` | 多帧数据处理、实体间交互（FindByName / SessionBlackboard）、业务延迟动作执行、存档持久化、实体销毁、控制台命令、观察者；新增错误路径：重复 kill 已死亡实体 |
| `GameplaySessionSwitchAndConcurrencyTests.cs` | 会话切换黑板隔离、同帧并发 spawn/kill、kill 后重 spawn、多后台会话并行处理；新增错误路径：kill 已收获实体 |
| `AdvancedGameplayIntegrationTests.cs` | 大量实体批量 spawn/kill（100 实体）、控制台命令路由（snd_count / bb_set/bb_get system 层）、实体数据直接 API round-trip、多策略实体组合（Lifecycle+Observer、Lifecycle+Active、三种类型全挂载）、多实体存档/加载状态保持；错误路径：request kill 未知实体、spawn 未注册策略索引 |
| `ActiveStrategyIntegrationTests.cs` | ActiveStrategy 在完整帧循环中的集成测试：直接 InvokeStrategy 调用、Process 触发自调用、跨实体 InvokeStrategy、ActiveStrategy 索引存档/加载持久化、AfterLoad 后 Invoke 验证、Lifecycle+Active 混合实体帧循环、动态 AddActiveStrategy/RemoveActiveStrategy 生命周期；错误路径：killed 实体上 InvokeStrategy、重复 AddActiveStrategy |
| `StateMachineIntegrationTests.cs` | 状态机在帧循环中的集成测试：Push/Pop 帧驱动、OnPushRuntime/OnPopRuntime 钩子触发、OnPopBeforeQuit 在 session destroy 时触发、状态机栈存档/加载 AfterLoad 恢复、多独立状态机栈、Lifecycle 策略跨帧 Push/Pop 状态；错误路径：session destroy 后操作状态机 |
| `ObserverTopologyIntegrationTests.cs` | 观察者拓扑在帧循环中的集成测试：mount 触发 OnMounted+OnDataChanged（带正确旧/新值）、unmount 停止通知、target kill 触发 OnUnmounted、数据变化新旧值正确性、多目标独立通知、帧驱动策略在 Process 中自动挂载观察者；错误路径：无效索引 mount、重复 mount、killed 实体 mount |
| `PlanningIntegrationTests.cs` | 意图驱动计划执行：intent 触发计划开始、两步骤计划完成、无 intent 不启动、数据属性键验证、多实体独立计划 |
| `StrategyStateSaveLoadIntegrationTests.cs` | 策略状态持久化：生命周期 count 状态 survive、实体数据+黑板 survive、重载后继续处理、20 实体批量无丢失、覆盖存档、多 session 全状态保留；错误路径：损坏 progress.json 导致加载失败 |

## GameplayIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `MultiFrameProcessing_AccumulatesData` | 策略每帧递增 count，RunFrames(10) 后 count=10 | architecture-overview: 帧循环 |
| `EntityInteraction_FindByName_ReadsPeerData` | 实体 A 在 Process 中通过 OwningSession.FindByName("peer") 读取实体 B 的 peer_value | ISessionRun.FindByName |
| `EntityInteraction_ViaBlackboard_TransfersDataBetweenFrames` | 实体 A 写入 SessionBlackboard → 同帧实体 B 读取 bridge_value | architecture-overview: 会话模型 |
| `DeferredAction_ExecutesAfterFlush` | 策略 EnqueueBusinessDeferred → DriveFrame FlushEndOfFrameDeferred 后 deferred_ran=true | Scheduling |
| `SaveDuringGameplay_PersistsToDisk` | 运行帧 → RequestSaveGameAuto → 验证 progress.json/level snd_scene.json 存在，实体数据不变 | persistence-flow |
| `EntityKill_BeforeDeadAndRemoval` | RequestKillEntity → DriveFrame → KillPendingAllSessions 收割，BeforeDead 触发，实体移除 | Runtime: SessionManager |
| `ConsoleCommand_DuringFrame` | 策略 TrySubmitConsoleCommand("snd_count") → DriveFrame ProcessPending → 控制台输出包含 "Snd count:" | console-commands |
| `FullGameLoopRoundTrip_SaveDisposeReload` | 运行帧 → 设定 session 数据 → Save → 销毁所有 session → Reload → 游戏 session 的 SessionBlackboard 数据恢复 | persistence-flow |
| `ObserverStrategy_MountAndNotify` | 实体 B MountObserverStrategy(实体A) → 实体A SetData("hp") → 观察者 OnDataChanged 触发 | snd-entity-model |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `MultiFrameProcessing_VariousFrameCounts_AccumulatesCorrectly` | (边界) 1/3/100 帧参数化 | 所有帧数下 count === frameCount |

## GameplaySessionSwitchAndConcurrencyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SwitchSession_BackgroundSessionBlackboard_Isolated` | 创建后台会话 → 设置各自黑板的独立 key → 验证互不污染 | SessionManager |
| `ConcurrentSpawnKill_SameFrame_AllCleanedUp` | 同时 spawn 两个实体 → 同一帧 kill 两个 → 验证 BeforeDead 触发两次 + 实体全部移除 | Runtime: SessionManager |
| `KillEntity_ThenRespawn_NewEntityIndependent` | 实体 kill → 同名重 spawn → 验证新实体 data 独立（不继承旧实体状态） | ISndEntity lifecycle |
| `MultipleBackgroundSessions_EntitiesProcessedInParallel` | 创建 3 个 session（1 前台 + 2 后台）→ 每 session 各 spawn 一个 FrameCounter → DriveFrame → 验证各自 count=1 | SessionManager: Multi-session |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ErrorPath_KillAlreadyKilledEntity_Throws` | kill 已收获的实体 | `InvalidOperationException` |

## 测试辅助设施

| 设施 | 位置 | 用途 |
|------|------|------|
| `GameplaySimulationHarness` | `TestSupport/GameplaySimulationHarness.cs` | 一键创建完整运行时：OrigoRuntime + SndContext + 后台游戏会话（syncProcess=true），提供 DriveFrame/RunFrames/SpawnEntity/RequestKillEntity/CreateBackgroundSession/GetEntityData/SubmitConsoleCommand/SetEntityData/InvokeEntityStrategy/MountObserver/SaveAndReload |
| `GameplaySimulationBuilder` | `TestSupport/GameplaySimulationHarness.cs` | Fluent Builder：WithStrategy 注册策略、WithSessionConfig 设置会话黑板 |
| `TestStrategies` | `TestSupport/TestStrategies.cs` | 共享抽象策略基类：`SharedFrameCounterStrategy`（AfterSpawn 初始化 count=0，Process 每帧递增）、`SharedEchoActiveStrategy`（Invoke 返回 input×2）、`SharedKillProbeStrategy`（BeforeDead 记录事件）、`SharedNoopLifecycleStrategy`（空生命周期）、`SharedNoopStateMachineStrategy`（空状态机）。各测试文件通过 `private sealed` 子类引用，赋予独立 `[StrategyIndex]`。 |
| `PeerLookupStrategy` | `GameplayIntegrationTests.cs` | Process 中通过 OwningSession.FindByName 查找对端实体并读取数据 |
| `BbWriterStrategy` | `GameplayIntegrationTests.cs` | Process 中向 OwningSession.SessionBlackboard 写入 bridge_value |
| `BbReaderStrategy` | `GameplayIntegrationTests.cs` | Process 中从 OwningSession.SessionBlackboard 读取 bridge_value 并存入实体 data |
| `DeferredProbeStrategy` | `GameplayIntegrationTests.cs` | Process 中 EnqueueBusinessDeferred 设置 deferred_ran=true |
| `ConsoleCommandStrategy` | `GameplayIntegrationTests.cs` | Process 中 TrySubmitConsoleCommand("snd_count")，验证命令行经 DriveFrame 处理 |
| `HpObserverIntegrationStrategy` | `GameplayIntegrationTests.cs` | ObserverStrategyBase：OnDataChanged 记录 "changed:{dataKey}"，验证观察者 Mount/Notify 机制 |
| `DataObserverIntegrationStrategy` | `AdvancedGameplayIntegrationTests.cs` | ObserverStrategyBase：ObserveData("count")，OnDataChanged 记录 "changed:{dataKey}"，用于多策略组合测试 |

## 使用模式

```csharp
var harness = GameplaySimulationHarness.Create()
    .WithStrategy(() => new CounterStrategy())
    .Build();

harness.SpawnEntity("counter", ["test.counter"]);

harness.RunFrames(10);

var count = harness.GetEntityData<int>("counter", "count");
Assert.Equal(10, count);
```

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 多实体批量 spawn + 帧处理的扩展场景（实体数量 > 100） | 未验证大量实体时帧循环的稳定性 | architecture-overview: 帧循环 |
| StrategyStateMachine 在帧循环中的跨实体状态机交互 | 未验证状态机变换触发的跨实体作用 | state-machine |
| 延迟动作在 session destroy 后的错误行为 | 未验证 EnqueueDeferred 在 dispose 后的抛出语义 | Scheduling |
| Save/load 的错误路径集成验证（缺失文件、损坏的 level 数据） | 大部分仅单元测试覆盖，缺少帧管线穿透验证 | persistence-flow |

---

[↑ 回到 Origo.Core.Tests](../README.md)
