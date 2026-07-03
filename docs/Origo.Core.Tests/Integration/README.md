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
| `GameplayIntegrationTests.cs` | 多帧数据处理、实体间交互（FindByName / SessionBlackboard）、业务延迟动作执行、存档持久化、实体销毁、控制台命令、观察者 |
| `GameplaySessionSwitchAndConcurrencyTests.cs` | 会话切换黑板隔离、同帧并发 spawn/kill、kill 后重 spawn、多后台会话并行处理 |
| `AdvancedGameplayIntegrationTests.cs` | 大量实体批量 spawn/kill（100 实体）、控制台命令路由（snd_count / bb_set/bb_get system 层）、实体数据直接 API round-trip、多策略实体组合（Lifecycle+Observer、Lifecycle+Active、三种类型全挂载）、多实体存档/加载状态保持、request kill 未知实体错误路径 |
| `ActiveStrategyIntegrationTests.cs` | ActiveStrategy 在完整帧循环中的集成测试：直接 InvokeStrategy 调用、Process 触发自调用、跨实体 InvokeStrategy、ActiveStrategy 索引存档/加载持久化、AfterLoad 后 Invoke 验证、Lifecycle+Active 混合实体帧循环、动态 AddActiveStrategy/RemoveActiveStrategy 生命周期 |

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

## GameplaySessionSwitchAndConcurrencyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SwitchSession_BackgroundSessionBlackboard_Isolated` | 创建后台会话 → 设置各自黑板的独立 key → 验证互不污染 | SessionManager |
| `ConcurrentSpawnKill_SameFrame_AllCleanedUp` | 同时 spawn 两个实体 → 同一帧 kill 两个 → 验证 BeforeDead 触发两次 + 实体全部移除 | Runtime: SessionManager |
| `KillEntity_ThenRespawn_NewEntityIndependent` | 实体 kill → 同名重 spawn → 验证新实体 data 独立（不继承旧实体状态） | ISndEntity lifecycle |
| `MultipleBackgroundSessions_EntitiesProcessedInParallel` | 创建 3 个 session（1 前台 + 2 后台）→ 每 session 各 spawn 一个 FrameCounter → DriveFrame → 验证各自 count=1 | SessionManager: Multi-session |

## 测试辅助设施

| 设施 | 位置 | 用途 |
|------|------|------|
| `GameplaySimulationHarness` | `TestSupport/GameplaySimulationHarness.cs` | 一键创建完整运行时：OrigoRuntime + SndContext + 后台游戏会话（syncProcess=true），提供 DriveFrame/RunFrames/SpawnEntity/RequestKillEntity/CreateBackgroundSession/GetEntityData/SubmitConsoleCommand/SetEntityData/InvokeEntityStrategy/MountObserver |
| `GameplaySimulationBuilder` | `TestSupport/GameplaySimulationHarness.cs` | Fluent Builder：WithStrategy 注册策略、WithSessionConfig 设置会话黑板 |
| `FrameCounterStrategy` | `GameplayIntegrationTests.cs` | AfterSpawn 初始化 count=0，Process 每帧 count++ |
| `PeerLookupStrategy` | `GameplayIntegrationTests.cs` | Process 中通过 OwningSession.FindByName 查找对端实体并读取数据 |
| `BbWriterStrategy` | `GameplayIntegrationTests.cs` | Process 中向 OwningSession.SessionBlackboard 写入 bridge_value |
| `BbReaderStrategy` | `GameplayIntegrationTests.cs` | Process 中从 OwningSession.SessionBlackboard 读取 bridge_value 并存入实体 data |
| `DeferredProbeStrategy` | `GameplayIntegrationTests.cs` | Process 中 EnqueueBusinessDeferred 设置 deferred_ran=true |
| `KillProbeIntegrationStrategy` | `GameplayIntegrationTests.cs` | BeforeDead 时记录 "before_dead" 事件，验证 Kill 收割触发钩子 |
| `ConsoleCommandStrategy` | `GameplayIntegrationTests.cs` | Process 中 TrySubmitConsoleCommand("snd_count")，验证命令行经 DriveFrame 处理 |
| `HpObserverIntegrationStrategy` | `GameplayIntegrationTests.cs` | ObserverStrategyBase：OnDataChanged 记录 "changed:{dataKey}"，验证观察者 Mount/Notify 机制 |
| `BatchFrameCounterStrategy` | `AdvancedGameplayIntegrationTests.cs` | AfterSpawn 初始化 count=0，Process 每帧 count++，用于大量实体和策略组合测试 |
| `DataObserverIntegrationStrategy` | `AdvancedGameplayIntegrationTests.cs` | ObserverStrategyBase：ObserveData("count")，OnDataChanged 记录 "changed:{dataKey}"，用于多策略组合测试 |
| `EchoActiveStrategy` | `AdvancedGameplayIntegrationTests.cs` | ActiveStrategyBase：Invoke 返回 input * 2（int 类型），用于多策略组合和 ActiveStrategy 集成测试 |

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
| ActiveStrategy 在帧循环中通过 InvokeStrategy 的调用模式 | 当前测试未覆盖 ActiveStrategy | strategy-testing |
| 跨 session 的实体交互（一个 session 的实体通过 SessionManager.TryGet 访问另一个 session 的实体） | 未验证跨 session 实体的策略互操作 | SessionManager |

---

[↑ 回到 Origo.Core.Tests](../README.md)
