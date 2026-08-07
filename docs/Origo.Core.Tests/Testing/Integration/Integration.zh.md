<!-- docsync-pair: Origo.Core.Tests/Testing/Integration/Integration -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 帧驱动游戏模拟集成测试

> [↑ 回到 Origo.Core.Tests](../../README.zh.md)
> [↔ 被测行为: usage/architecture-overview](../../../usage/architecture-overview.zh.md)
> [↔ 被测模块: Origo.Core/Runtime](../../../Origo.Core/Runtime/README.zh.md)

## 被测行为概览

验证 `IOrigoFrameDriver.DriveFrame(delta)` 驱动的完整帧模拟管线：
从 `OrigoRuntime` → `SndContext` → `ProgressRun` → `SessionManager` → `SessionRun`
四层运行时在真实 `SndEntity` 实体与策略的参与下，按帧循环顺序执行实体处理、
业务延迟队列、实体收割和系统延迟队列。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GameplayIntegrationTests.cs` | 多帧数据处理、实体间交互（FindByName / SessionBlackboard）、业务延迟动作执行、存档持久化、实体销毁、控制台命令、观察者；错误路径覆盖：重复 kill 已死亡实体 |
| `GameplaySessionSwitchAndConcurrencyTests.cs` | 会话切换黑板隔离、同帧并发 spawn/kill、kill 后重 spawn、多后台会话并行处理；错误路径覆盖：kill 已收获实体 |
| `AdvancedGameplayIntegrationTests.cs` | 大量实体批量 spawn/kill（100 实体）、控制台命令路由（snd_count / bb_set/bb_get system 层）、实体数据直接 API round-trip、多策略实体组合（Lifecycle+Observer、Lifecycle+Active、三种类型全挂载）、多实体存档/加载状态保持；错误路径：request kill 未知实体、spawn 未注册策略索引 |
| `ActiveStrategyIntegrationTests.cs` | ActiveStrategy 在完整帧循环中的集成测试：直接 InvokeStrategy 调用、Process 触发自调用、跨实体 InvokeStrategy、ActiveStrategy 索引存档/加载持久化、AfterLoad 后 Invoke 验证、Lifecycle+Active 混合实体帧循环、动态 AddActiveStrategy/RemoveActiveStrategy 生命周期；错误路径：killed 实体上 InvokeStrategy、重复 AddActiveStrategy |
| `StateMachineIntegrationTests.cs` | 状态机在帧循环中的集成测试：Push/Pop 帧驱动、OnPushRuntime/OnPopRuntime 钩子触发、OnPopBeforeQuit 在 session destroy 时触发、状态机栈存档/加载 AfterLoad 恢复、多独立状态机栈、Lifecycle 策略跨帧 Push/Pop 状态；错误路径：session destroy 后操作状态机 |
| `ObserverTopologyIntegrationTests.cs` | 观察者拓扑在帧循环中的集成测试：mount 触发 OnMounted+OnDataChanged（带正确旧/新值）、unmount 停止通知、target kill 触发 OnUnmounted、数据变化新旧值正确性、多目标独立通知、帧驱动策略在 Process 中自动挂载观察者；错误路径：无效索引 mount、重复 mount、killed 实体 mount |
| `PlanningIntegrationTests.cs` | 意图驱动计划执行：intent 触发计划开始、两步骤计划完成、无 intent 不启动、数据属性键验证、多实体独立计划 |
| `StrategyStateSaveLoadIntegrationTests.cs` | 策略状态持久化：生命周期 count 状态 survive、实体数据+黑板 survive、重载后继续处理、20 实体批量无丢失、覆盖存档、多 session 全状态保留；错误路径：损坏 progress.json 导致加载失败 |
| `ErrorPathIntegrationTests.cs` | 延迟动作帧内正确执行；错误路径：损坏 session.json 加载、损坏 snd_scene.json 加载、不存在的存档加载 |

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
| `CrossSession_EntityReadsPeerInAnotherSession` | 后台会话实体运行帧后，经 TryGetSession + FindByName 可读取其数据（count 跨帧累计） | SessionManager: Multi-session |
| `BackgroundSession_SaveLoad_IndependentEntityState` | 前台与后台会话实体/黑板独立存档加载：重载后前台 count 与后台 count/黑板值均恢复 | persistence-flow |
| `BackgroundSession_KillEntities_DuringForegroundPlay` | 前台播放期间收割后台会话实体（BeforeDead 触发、实体移除），前台实体不受影响 | Runtime: SessionManager |
| `MultipleBackgroundSessions_SaveLoadCycle` | 多个后台会话 + 前台会话存档重载后全部恢复（实体 count 与各自会话黑板独立保留） | persistence-flow |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ErrorPath_KillAlreadyKilledEntity_Throws` | kill 已收获的实体 | `InvalidOperationException` |

## AdvancedGameplayIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `BatchSpawn_100Entities_AllProcessed` | 批量 spawn 100 实体后运行 5 帧，全部实体的 count=5 | architecture-overview: 帧循环 |
| `BatchSpawn_ThenBatchKill_AllCleanedUp` | 100 实体批量 spawn 后同帧批量 kill，DriveFrame 后全部收割移除 | Runtime: SessionManager |
| `ConsoleCommand_SndCount_PublishesOutput` | 提交 snd_count 命令后控制台输出包含 "Snd count:" | console-commands |
| `ConsoleCommand_BbSetSystemLayer_RoundTrip` | bb_set/bb_get system 层命令：写入 int/string 经 SystemBlackboard 读回，bb_get 输出值 | console-commands |
| `EntityDataSetGet_DirectAPI_RoundTrip` | 实体 SetData/GetData/TryGetData 直接 API 往返（int/string/bool） | snd-entity-model: TypedData |
| `MultiStrategyEntity_LifecyclePlusObserver` | Lifecycle+Observer 混合实体：帧处理递增 count 并触发观察者数据变更 | snd-entity-model: 观察者 |
| `MultiStrategyEntity_LifecyclePlusActive` | Lifecycle+Active 混合实体：帧处理累计 count 与 InvokeStrategy 同时工作 | snd-entity-model |
| `MultiStrategyEntity_AllThreeTypes` | 三种策略类型（Lifecycle+Observer+Active）全挂载实体正常帧处理、通知与 Invoke | snd-entity-model |
| `SaveLoad_MultipleEntities_StatePreserved` | 10 实体 + 会话黑板存档重载后实体数、count、tag、黑板值全部恢复 | persistence-flow |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ErrorPath_RequestKillUnknownEntity_Throws` | RequestKillEntity 不存在的实体 | InvalidOperationException |
| `ErrorPath_SpawnWithUnregisteredStrategyIndex_Throws` | Spawn 使用未注册的策略索引 | 抛异常 |

## ActiveStrategyIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `InvokeStrategy_DirectCall_ReturnsResult` | 实体动态 AddActiveStrategy 后直接 InvokeStrategy 返回策略结果（21→42） | snd-entity-model: ActiveStrategy |
| `InvokeStrategy_ProcessTriggersActive_WithinFrame` | Lifecycle 策略在 Process 中调用自身 InvokeStrategy，结果写入实体数据 | snd-entity-model: ActiveStrategy |
| `InvokeStrategy_PeerEntityActiveStrategy_CrossEntity` | Process 中经 OwningSession.FindByName 调用其他实体的 ActiveStrategy | snd-entity-model: ActiveStrategy |
| `ActiveStrategyIndices_SaveLoad_Persisted` | 动态挂载的 ActiveStrategy 索引存档/重载后仍可 Invoke（结果正确） | persistence-flow |
| `ActiveStrategy_AfterLoad_InvokeWorks` | 重载后实体 ActiveStrategy 可用且实体数据保持 | persistence-flow |
| `HybridEntity_LifecycleProcessAndActiveInvoke` | Lifecycle+Active 混合实体：帧循环 Process 累计 count，Invoke 同时可用 | snd-entity-model |
| `ActiveStrategy_DynamicAddRemove_InFrameLoop` | 帧循环中动态 Add/RemoveActiveStrategy：添加前 Invoke 抛异常、添加后可用、移除后再次抛异常 | snd-entity-model |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ErrorPath_InvokeActiveStrategyOnKilledEntity_Throws` | 实体被 kill 后 InvokeStrategy | 抛异常 |
| `ErrorPath_AddDuplicateActiveStrategy_Throws` | 重复 AddActiveStrategy | 抛异常 |

## StateMachineIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `StateMachine_PushPop_InFrameLoop` | 帧循环中 Push/Pop 状态机栈，Peek 反映正确栈顶 | state-machine |
| `StateMachine_OnPushHook_FiresCorrectly` | Push 触发 OnPushRuntime 钩子（记录 after top 值） | state-machine: Push |
| `StateMachine_OnPopHook_FiresCorrectly` | TryPopRuntime 触发 OnPopRuntime 钩子，栈空后 Peek 返回 false | state-machine: TryPopRuntime |
| `StateMachine_OnPopBeforeQuit_FiresOnSessionDestroy` | 会话销毁时对栈内状态触发 OnPopBeforeQuit | state-machine: TryPopOnQuit |
| `StateMachine_SaveLoad_PreservesStack` | 存档重载后栈保留（AfterLoad 钩子按层触发、可继续 Pop） | state-machine: 读档恢复 |
| `StateMachine_SaveLoad_AfterLoadHookFiresOncePerLayer` | 重载后每层栈恰好触发一次 OnPushAfterLoad（无重复冲刷） | state-machine: 读档恢复 |
| `StateMachine_MultipleEntities_IndependentStacks` | 同一会话多个状态机栈相互独立 | state-machine |
| `StateMachine_EntityLifecycleStrategy_PushesAndPopsState` | Lifecycle 策略跨帧 Push/Pop 实体状态机 | state-machine |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ErrorPath_PushStateMachineAfterSessionDestroy_Throws` | 会话销毁后操作状态机（Peek/Push） | ObjectDisposedException |

## ObserverTopologyIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Observer_Mount_TriggersOnMountedAndDataChange` | Mount 触发 OnMounted，目标数据变更触发 OnDataChanged（新值正确） | snd-entity-model: 观察者 |
| `Observer_Unmount_StopsNotifying` | Unmount 后目标数据变更不再通知 | snd-entity-model: 观察者 |
| `Observer_TargetKilled_TriggersOnUnmounted` | 目标实体被 kill 触发 OnUnmounted | snd-entity-model: 观察者 |
| `Observer_OldAndNewValues_CorrectOnChange` | 连续变更时 OnDataChanged 收到正确 oldValue/newValue 序列 | snd-entity-model: 观察者 |
| `Observer_MultipleTargets_NotifiedIndependently` | 观察者同时观察多目标，各自独立通知 | snd-entity-model: 观察者 |
| `Observer_FrameDriven_StrategyMountsObserverInProcess` | Lifecycle 策略在 AfterSpawn 中自动挂载观察者，帧循环后通知正常 | snd-entity-model: 观察者 |
| `Observer_Bindings_RestoredAcrossSaveAndReload` | 观察者绑定存档重载后恢复，数据变更仍通知 | persistence-flow |
| `Observer_OnMounted_FiresAgainAfterReload` | 重载恢复绑定后 OnMounted 再次触发 | persistence-flow |
| `Observer_OnUnmounted_FiresWhenSessionIsDestroyed` | 会话销毁时观察者收到 OnUnmounted | snd-entity-model: 观察者 |
| `Observer_TargetDataNoLongerNotifiesAfterSessionDestroyed` | 会话销毁后目标数据变更不再通知 | snd-entity-model: 观察者 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Observer_MountWithInvalidIndex_Throws` | 未注册的观察者索引挂载 | InvalidOperationException（"not found"） |
| `Observer_DuplicateMount_Throws` | 重复挂载相同 (observer, target, index) | InvalidOperationException（"already mounted"），OnMounted 不重复触发 |
| `Observer_MountToKilledEntity_Throws` | 挂载到已 kill 的目标实体 | InvalidOperationException（"pending kill"） |
| `Observer_KilledObserverCannotMount_Throws` | 已被 kill 的观察者实体发起挂载 | InvalidOperationException（"pending kill"） |
| `Observer_MountAcrossSessions_Throws` | 跨会话挂载（其他会话的实体挂载前台实体） | InvalidOperationException（"different sessions"） |

## PlanningIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `PlanExecution_SetIntent_StartsPlanInFrameLoop` | 设置意图数据后帧驱动启动计划（plan_step=step_a、action_status=executing） | Planning: PlanExecutionStrategyBase |
| `PlanExecution_CompletePlan_InFrameLoop` | 动作完成后帧驱动推进计划步骤直至意图完成（task_status=completed） | Planning: PlanExecutionStrategyBase |
| `PlanExecution_WithoutIntent_DoesNotStart` | 未设置意图时计划不启动（无 plan_step/task_status 数据） | Planning: PlanExecutionStrategyBase |
| `PlanExecution_DataAttributeKeys_AreSetCorrectly` | 计划启动后 plan_step 与 action_index 数据键正确设置 | Planning: PlanExecutionStrategyBase |
| `PlanExecution_MultipleEntities_IndependentPlans` | 多实体计划相互独立，一个实体推进不影响另一个 | Planning: PlanExecutionStrategyBase |

## StrategyStateSaveLoadIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `LifecycleStrategy_StateSurvivesSaveLoad` | Lifecycle 策略 count 状态与实体数据存档重载后保持，重载后继续帧处理 | persistence-flow |
| `EntityDataAndBlackboard_BothSurviveSaveLoad` | 实体数据与 SessionBlackboard（int/string）存档重载后均恢复 | persistence-flow |
| `SaveLoad_ThenContinue_EntityStillProcesses` | 重载后继续运行帧，实体 count 从恢复值继续累计 | persistence-flow |
| `SaveLoad_NoLossOfEntities` | 20 实体批量存档重载无丢失（实体数、count、id 均正确） | persistence-flow |
| `SaveTwice_SecondOverwrites_StateCorrect` | 同槽位二次保存覆盖：重载得到第二次状态（count 与黑板 version） | persistence-flow |
| `SaveLoad_MultipleSessions_AllStatePreserved` | 前台 + 后台会话实体与黑板存档重载后全部状态保留 | persistence-flow |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ErrorPath_LoadCorruptSave_Throws` | progress.json 损坏时加载 | Flush 时抛异常（fail-fast） |

## ErrorPathIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DeferredAction_ExecutesAndFlushesThroughDriveFrame` | Process 中入队的延迟动作经 DriveFrame 冲刷执行（count 累计 + deferred_ran=true） | Scheduling |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ErrorPath_LoadNonexistentSave_Throws` | 加载不存在的存档 | Flush 时抛异常（消息含 "nonexistent"） |
| `ErrorPath_LoadSaveWithCorruptedSessionFile_Throws` | session.json 损坏时加载 | Flush 时抛异常 |
| `ErrorPath_LoadSaveWithCorruptedSndScene_Throws` | snd_scene.json 损坏时加载 | Flush 时抛异常 |

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
| `BatchFrameCounterStrategy` | `AdvancedGameplayIntegrationTests.cs` | SharedFrameCounterStrategy 子类，批量 spawn/kill 帧计数 |
| `EchoActiveStrategy` | `AdvancedGameplayIntegrationTests.cs` | SharedEchoActiveStrategy 子类（input×2），三种策略类型组合测试 |
| `EchoActiveStrategy` | `ActiveStrategyIntegrationTests.cs` | SharedEchoActiveStrategy 子类（input×2），验证 ActiveStrategy Invoke/存档持久化 |
| `SelfInvokeStrategy` | `ActiveStrategyIntegrationTests.cs` | Process 中调用自身 InvokeStrategy 并写入结果 |
| `PeerInvokeStrategy` | `ActiveStrategyIntegrationTests.cs` | Process 中经 FindByName 调用对端实体 ActiveStrategy 并写入结果 |
| `AdvFrameCounterStrategy` | `ActiveStrategyIntegrationTests.cs` | SharedFrameCounterStrategy 子类，混合实体帧计数 |
| `ErrorPathFrameCounterStrategy` | `ErrorPathIntegrationTests.cs` | SharedFrameCounterStrategy 子类，损坏存档加载场景帧计数 |
| `DeferredCounterStrategy` | `ErrorPathIntegrationTests.cs` | Process 每帧递增 count，count>0 时入队延迟动作置 deferred_ran=true |
| `BlackboardMarkerStrategy` | `GameplaySessionSwitchAndConcurrencyTests.cs` | SharedNoopLifecycleStrategy 子类，会话切换黑板隔离场景占位 |
| `KillableTestStrategy` | `GameplaySessionSwitchAndConcurrencyTests.cs` | SharedKillProbeStrategy 子类，BeforeDead 记录事件 |
| `FrameCounterStrategy` | `GameplaySessionSwitchAndConcurrencyTests.cs` | SharedFrameCounterStrategy 子类（TestStrategyIndices.FrameCounter），多会话并行/存档帧计数 |
| `TopologyObserverStrategy` | `ObserverTopologyIntegrationTests.cs` | ObserverStrategyBase 观察 hp，记录 OnMounted/OnDataChanged/OnUnmounted 事件 |
| `ValueCapturingObserverStrategy` | `ObserverTopologyIntegrationTests.cs` | ObserverStrategyBase 观察 hp，记录 oldValue/newValue |
| `TargetAwareObserverStrategy` | `ObserverTopologyIntegrationTests.cs` | ObserverStrategyBase 观察 hp，记录 TargetName |
| `AutoMountObserverLifecycleStrategy` | `ObserverTopologyIntegrationTests.cs` | AfterSpawn 中自动挂载观察者到 "target"，验证帧驱动挂载 |
| `TwoStepPlanStrategy` | `PlanningIntegrationTests.cs` | PlanExecutionStrategyBase 子类：intent "build"/"repair" 分 step_a→step_b 两步骤计划 |
| `NoopActionStrategy` | `PlanningIntegrationTests.cs` | SharedNoopLifecycleStrategy 子类，计划 Action 占位 |
| `PushTrackingStateMachineStrategy` | `StateMachineIntegrationTests.cs` | SharedNoopStateMachineStrategy 子类，帧循环 Push/Pop 栈驱动 |
| `HookRecordingStateMachineStrategy` | `StateMachineIntegrationTests.cs` | 记录 on_push_runtime/on_push_after_load/on_pop_runtime/on_pop_before_quit 事件 |
| `SmPushingLifecycleStrategy` | `StateMachineIntegrationTests.cs` | Lifecycle 策略：AfterSpawn Push "active"，帧数达 3 后 Pop 并 Push "idle" |
| `StateFrameCounterStrategy` | `StrategyStateSaveLoadIntegrationTests.cs` | SharedFrameCounterStrategy 子类，策略状态存档/加载帧计数 |

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

---

[↑ 回到 Origo.Core.Tests](../../README.zh.md)
