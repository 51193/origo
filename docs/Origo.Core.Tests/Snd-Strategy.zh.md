<!-- docsync-pair: Origo.Core.Tests/Snd-Strategy -->
<!-- docsync-revision: 13 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# SND 策略 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Snd/Strategy](../Origo.Core/Snd/Strategy/README.zh.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.zh.md)

## 被测行为概览

验证 SND 策略系统的全部行为：策略优先级排序、池引用计数/回收、实体策略的 8 个生命周期钩子、主动策略的 Invoke 调用、观察者策略的挂载/卸载/数据变更通知/持久化/拓扑查询、策略注册时的类型安全校验。

`SndStrategyPerformanceTests` 中的三个性能测试使用 `Stopwatch` + `PerfReporter` 测量吞吐/分配并附带正确性断言，标记 `[Trait("Category","Benchmark")]`，由 `scripts/benchmark.sh` 执行，不进入常规功能测试管线。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `ActiveStrategyTests.cs` | 主动策略 Invoke 调用、Spawn/Load 恢复、Quit/Dead 释放、动态增删、序列化、注册校验、Entity/Active 混合场景 |
| `ActiveStrategyJsonBaseTests.cs` | ActiveStrategyJsonBase JSON 契约：输入反序列化/结果序列化、错误输入返回 err 结果、裸字符串结果直通、null 输入执行、泛型扩展调用往返 |
| `LifecycleStrategyBaseTests.cs` | 默认钩子不变更数据；Process 中 Add/Kill/SelfKill/OtherKill 的并发语义；AfterAdd 失败回滚；不存在策略操作的安全处理 |
| `ObserverStrategyTests.cs` | 观察者注册/无状态校验；Mount/Unmount 生命周期与参数正确性；数据变更通知（正确键/非观察键/卸载后）及新旧值；多键观察；序列化（ObserverIndices 填充/空绑定/分组）；Dead/Quit 释放与 OnUnmounted；属性反射提取；跨实体挂载拒绝；null/空/未知参数防御；RecoverBindings 容错；Has/Remove 拓扑查询；Teardown/KillPending/ClearAll 清理路径 |
| `StrategyPriorityTests.cs` | 策略按 Priority 升序排列、同优先级按插入顺序 FIFO、所有生命周期钩子遵循优先级、序列化/恢复保持顺序 |
| `StrategyPoolTypeSafetyAndExtensionTests.cs` | 策略池类型分支安全（泛型 GetStrategy 类型不匹配不泄漏 ref count）、StackStateMachine 二阶段获取失败回滚、第三领域根基类扩展、RecoverStrategiesOnly 拒绝非 Lifecycle 策略 |
| `SndStrategyPoolLeakDetectionTests.cs` | 策略池泄漏检测：实体正常释放/异常中途失败时策略引用计数归零、无泄漏；LogPoolLeaks 无残留告警 |
| `SndStrategyPerformanceTests.cs` | 策略池 Get/Release 吞吐、Process 策略数缩放、TriggerAll ToArray 分配（标记 `[Trait("Category","Benchmark")]`，由 `scripts/benchmark.sh` 执行） |

## ActiveStrategyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Invoke_ReturnsResult` | Invoke 返回 ActiveStrategy 强类型结果 | snd-entity-model |
| `Invoke_EntityPassedCorrectly` | Invoke 传入 entity 名正确（input="get_name" 返回实体名） | snd-entity-model |
| `Invoke_InputPassedCorrectly` | Invoke 传入 input 参数正确路由到策略 | snd-entity-model |
| `Spawn_RecoversActiveStrategies` | Spawn 后 ActiveStrategy 可用 | snd-entity-model |
| `Load_RecoversActiveStrategies` | Load 后 ActiveStrategy 可用 | snd-entity-model |
| `Quit_ReleasesAllActiveStrategies` | Quit 后 Invoke 抛出（策略已释放） | snd-entity-model |
| `Dead_ReleasesAllActiveStrategies` | Dead 后 Invoke 抛出（策略已释放） | snd-entity-model |
| `AddActiveStrategy_Then_Invoke_Works` | 动态添加 ActiveStrategy 后 Invoke 成功 | snd-entity-model |
| `SerializeMetaData_IncludesActiveIndices` | Save 后 MetaData 包含 ActiveIndices | snd-entity-model |
| `SerializeMetaData_EntityAndActive_Separated` | LifecycleIndices 与 ActiveIndices 正确分离，互不包含 | snd-entity-model |
| `SerializeMetaData_DynamicAdd_Then_Serialized` | 动态添加的 ActiveStrategy 出现在序列化结果中 | snd-entity-model |
| `SerializeMetaData_DynamicRemove_NotSerialized` | 动态移除后序列化结果为空 | snd-entity-model |
| `SameEntity_HasBothTypeStrategies` | 同一实体同时挂载 LifecycleStrategy 和 ActiveStrategy，Process 与 Invoke 均正常 | snd-entity-model |
| `RemoveLifecycleStrategy_LeavesActiveStrategy` | 移除 LifecycleStrategy 后 ActiveStrategy Invoke 仍可用 | snd-entity-model |
| `RemoveActiveStrategy_LeavesLifecycleStrategy` | 移除 ActiveStrategy 后 LifecycleStrategy Process 仍可用 | snd-entity-model |
| `ActiveStrategy_AutoDiscovered` | 注册后可通过 GetRegisteredStrategyIndices() 发现 | snd-entity-model |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Invoke_UnregisteredIndex_Throws` | 调用未注册索引 | InvalidOperationException（含索引名） |
| `Invoke_LifecycleStrategyIndex_Throws` | 用 LifecycleStrategy 索引调用 Invoke | InvalidOperationException |
| `Load_ActiveIndexWithNonActiveType_Throws` | ActiveIndices 中包含非 ActiveStrategyBase 类型 | InvalidOperationException（含索引名和类型名） |
| `Load_ActiveIndexWithNonActiveType_RollsBackAcquiredActives` | 失败前已获取的 ActiveStrategy 必须回滚 | 验证 InvalidOperationException + 失败后 Invoke 再次抛出 |
| `AddActiveStrategy_Duplicate_Throws` | 重复添加同名 ActiveStrategy | InvalidOperationException（"already attached"） |
| `AddActiveStrategy_NonActiveType_Throws` | 添加非 ActiveStrategyBase 类型 | InvalidOperationException |
| `AddActiveStrategy_NullOrWhitespace_Throws` | null 或空白索引 | ArgumentException |
| `RemoveActiveStrategy_Then_Invoke_Throws` | 移除后调用 Invoke | InvalidOperationException |
| `ActiveStrategy_StatelessnessEnforced` | 注册有实例字段（_counter）的 ActiveStrategy | InvalidOperationException（"invalid instance members"，含字段名） |
| `ActiveStrategy_MissingAttribute_Throws` | 注册无 [StrategyIndex] 的 ActiveStrategy | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `RemoveActiveStrategy_NotExists_Throws` | 移除不存在的 ActiveStrategy | 抛 `InvalidOperationException`（fail-fast） |

## ActiveStrategyJsonBaseTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Invoke_ValidJsonInput_DeserializesAndSerializesResult` | 合法 JSON 输入反序列化为强类型后传给 Execute，返回值序列化为 JSON 字符串 | Strategy README: ActiveStrategyJsonBase |
| `Invoke_StringResult_IsSerializedAsJsonString` | Ok 字符串结果序列化为 JSON 字符串（`"ok"`） | Strategy README: ActiveStrategyJsonBase |
| `Invoke_ErrorResult_IsSerializedAsJsonString` | Err 结果序列化为 JSON 字符串（`"err:invalid"`） | Strategy README: ActiveStrategyJsonBase |
| `Invoke_InvalidJsonInput_ReturnsErrorResult` | 非法 JSON 输入返回 `"err:Invalid request"` 错误结果而非抛异常 | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NonStringInput_ReturnsErrorResult` | 非字符串输入返回 `"err:Invalid request"` 错误结果 | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NullResult_SerializesNull` | Execute 返回 null 时序列化为 JSON 字面量 `null` | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NullInput_ExecutesWithDefault` | null 输入以默认值执行（int 默认 0，结果为 `"0"`） | Strategy README: ActiveStrategyJsonBase |
| `Invoke_StringReferenceTypeInput_RoundTrips` | 字符串引用类型输入往返保持（`"hello"` → `"hello"`） | Strategy README: ActiveStrategyJsonBase |
| `Invoke_NullJsonInput_ExecutesWithNullReference` | JSON 字面量 `null` 输入以 null 引用执行并序列化为 `null` | Strategy README: ActiveStrategyJsonBase |
| `GenericInvoke_JsonBaseStrategy_RoundTripsThroughExtensions` | 泛型 InvokeStrategy<TestPayload,TestPayload> 经 JSON 基类完整往返 | Snd README: ActiveStrategyExtensions |
| `GenericInvoke_BareStringResult_ReturnsStringAsIs` | 返回裸字符串的旧策略经泛型调用原样返回，不抛 JSON 异常 | Snd README: ActiveStrategyExtensions |
| `GenericInvoke_ErrorBareString_ReturnsStringAsIs` | 裸字符串 err 结果（`"err:no gold"`）原样返回 | Snd README: ActiveStrategyExtensions |

## LifecycleStrategyBaseTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DefaultHooks_DoNotMutateEntityData` | 全部 8 个默认生命周期钩子不改变实体数据 | snd-entity-model: 策略生命周期钩子 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `AddStrategy_WhenAfterAddThrows_RollsBackInsertionAndPoolReference` | 策略 AfterAdd 钩子抛出 InvalidOperationException | 策略插入回滚，池引用归还，后续 Process 不执行该策略 |
| `AddStrategy_SameIndexTwice_Throws` | 对已挂载的策略索引重复 AddStrategy | InvalidOperationException（"already mounted"） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Process_AddsNewStrategy_DoesNotThrow` | Process 中调用 AddStrategy 添加新策略 | 不抛异常 |
| `Process_KillsItself_MarksEntity` | Process 中调用 RequestKillEntity(self) | 实体被标记 IsPendingKill |
| `Process_KillsOtherEntity_MarksTargetEntity` | Process 中调用 RequestKillEntity("B") | 目标实体被标记 IsPendingKill，当前实体不受影响 |
| `Process_RequestKillDuringProcess_RemainingStrategiesStillExecuted` | 第一个策略 Kill 自己后，同实体上后续策略仍执行 | KillSelfRecordingStrategy 先执行且记录，ProcessCalledStrategy 随后仍执行 |
| `Remove_NonexistentStrategy_Throws` | 移除不存在的策略 | 抛 `InvalidOperationException`（fail-fast） |

## ObserverStrategyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ObserverStrategy_CanBeRegistered` | 观察者策略可通过 RegisterStrategy 注册 | snd-entity-model: 观察者 |
| `Mount_TriggersOnMounted_WithCorrectParameters` | Mount 触发 OnMounted，Entity 和 Target 参数正确 | snd-entity-model: 观察者 |
| `Unmount_TriggersOnUnmounted_WithCorrectParameters` | Unmount 触发 OnUnmounted，参数正确 | snd-entity-model: 观察者 |
| `SetData_TriggersOnDataChanged_ForObservedKey` | 设置观察键（character.hp）触发 OnDataChanged | snd-entity-model: 观察者 |
| `SetData_DoesNotTrigger_ForUnobservedKey` | 设置非观察键（character.mp）不触发 | snd-entity-model: 观察者 |
| `SetData_DoesNotTrigger_AfterUnmount` | Unmount 后设置观察数据键不再触发回调 | snd-entity-model: 观察者 |
| `SetData_TriggersForMultipleKeys` | 多键观察（hp、mp）分别触发对应回调 | snd-entity-model: 观察者 |
| `SetData_OldAndNewValuesCorrect` | OnDataChanged 收到正确的 oldValue 和 newValue | snd-entity-model: 观察者 |
| `BuildMetaData_IncludesObserverBindings` | Save 后 MetaData 包含 ObserverIndices（Target + ObserverIndices） | snd-entity-model: 观察者 |
| `BuildMetaData_EmptyBindings_WhenNoObservers` | 无观察者时 ObserverIndices 为空列表 | snd-entity-model: 观察者 |
| `BuildMetaData_MultipleTargets_GroupedCorrectly` | 多个观察者策略挂载到同一 target 时合并为一条 ObserverBinding | snd-entity-model |
| `Dead_ReleasesObserverStrategies` | Dead 后不再触发数据变更通知 | snd-entity-model: 观察者 |
| `Dead_TriggersOnUnmounted` | Dead 触发 OnUnmounted | snd-entity-model: 观察者 |
| `ObserveDataAttribute_ExtractsKeys` | 反射提取 [ObserveData] 属性声明的数据键 | Strategy README: ObserverStrategyMetadata |
| `ObserveDataAttribute_MultipleKeys` | 多个 [ObserveData] 属性全部正确提取 | Strategy README: ObserverStrategyMetadata |
| `ObserveDataAttribute_NoAttributes_ReturnsEmpty` | 无 [ObserveData] 属性时返回空集合 | Strategy README: ObserverStrategyMetadata |
| `MountObserverStrategy_WithSelfTargetName_Succeeds` | 用自身实体名挂载观察者成功 | snd-entity-model: 观察者 |
| `Quit_TriggersOnUnmounted` | Quit 触发 OnUnmounted | snd-entity-model: 观察者 |
| `DeepClone_PreservesObserverBindings` | SndMetaData.DeepClone() 保持 ObserverIndices | snd-entity-model: 观察者 |
| `SaveSingle_ThenRecover_PreservesObserverBindings` | Save → 新实体 Spawn + RecoverBindingsFor 后数据变更通知正常 | snd-entity-model: 观察者 |
| `GetObserverNamesTargeting_ExistingTarget_ReturnsTrue` | 已挂载观察者时 GetObserverNamesTargeting 返回观察者名 | Strategy README: ObserverTopology |
| `GetObserverNamesTargeting_NonexistentTarget_ReturnsFalse` | 不存在目标绑定时返回空集合 | Strategy README: ObserverTopology |
| `RemoveAllObserverBindingsTargeting_ClearsBindings` | RemoveBindingsTargetingFor 清空指定 target 的全部绑定 | Strategy README: ObserverTopology |
| `TeardownOutgoingObserverBindings_TriggersOnUnmounted` | TeardownOutgoingFor 触发 OnUnmounted | Strategy README: ObserverTopology |
| `DataChange_OnlyTargetEntityNotified` | 数据变更仅通知观察目标实体的观察者（EntityName 和 TargetName 均为目标实体） | snd-entity-model: 观察者 |
| `BuildObserverBindings_TwoTargets_GroupsCorrectly` | BuildBindingsFor 按 target 正确分组 | Strategy README: ObserverTopology |
| `OnDataChanged_OldAndNewValues_Correct` | OnDataChanged 参数中 oldValue=100、newValue=50 | snd-entity-model: 观察者 |
| `GetObserverNamesTargeting_MountedObserver_ReturnsObserverName` | 已挂载观察者时 GetObserverNamesTargeting 返回观察者名 | Strategy README: ObserverTopology |
| `GetObserverNamesTargeting_NoBindings_ReturnsEmpty` | 无任何绑定时 GetObserverNamesTargeting 返回空（含未知目标名） | Strategy README: ObserverTopology |
| `GetObserverNamesTargeting_AfterUnmount_IndexCleared` | Unmount 后 GetObserverNamesTargeting 不再返回该观察者名 | Strategy README: ObserverTopology |
| `MountObserverStrategy_ByEntityOverload_Works` | 以实体重载挂载观察者到其他实体，目标数据变更触发回调且 Entity/Target 参数正确 | snd-entity-model: 观察者 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ObserverStrategy_StatelessEnforcement` | 注册有实例字段（_counter）的观察者策略 | InvalidOperationException（"invalid instance members"） |
| `ObserverStrategy_MissingAttribute_Throws` | 注册无 [StrategyIndex] 的观察者 | InvalidOperationException |
| `Mount_WhenOnMountedThrows_RollsBackAndReturnsToPool` | OnMounted 抛出 InvalidOperationException | 数据订阅回滚，后续 SetData 不触发回调，策略归还池 |
| `MountObserverStrategy_WithDifferentTargetName_Throws` | 目标名不等于自身实体名时挂载 | InvalidOperationException（"Cross-entity"） |
| `Mount_NullTargetName_Throws` | null 目标名 | InvalidOperationException |
| `Mount_EmptyObserverIndex_Throws` | 空字符串观察者索引 | ArgumentException |
| `Mount_UnknownObserverIndex_Throws` | 未注册的观察者索引 | InvalidOperationException |
| `MountObserverStrategy_ByEntityOverload_NullTarget_Throws` | 实体重载的 target 为 null | ArgumentNullException |
| `Mount_WhenGetStrategyThrows_PropagatesOriginalError` | 获取观察者策略失败 | 原始 InvalidOperationException 传播（含索引名） |
| `Unmount_WhenOnUnmountedThrows_PoolReferenceStillReleased` | OnUnmounted 钩子抛出 InvalidOperationException | 异常传播且策略仍归还池（LogPoolLeaks 无泄漏告警） |
| `FullCleanup_NullTargetEntity_ThrowsInvalidOperation` | FullCleanup 传入 null TargetEntity | InvalidOperationException（消息含 "TargetEntity"） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Mount_Duplicate_Throws` | 重复挂载同一观察者到同一目标 | InvalidOperationException（重复挂载拒绝） |
| `Unmount_NotMounted_Throws` | Unmount 未挂载的绑定 | InvalidOperationException |
| `NoDataKeyObserver_CanMountAndUnmount` | 无 [ObserveData] 属性的观察者挂载/卸载 | 不抛异常 |
| `RecoverBindings_TargetNotFound_Throws` | RecoverBindingsFor 时 resolveTarget 返回 null | InvalidOperationException（悬空绑定使加载失败） |
| `RecoverBindings_EmptyTarget_Throws` | 存档绑定目标为 null/空白 | InvalidOperationException |
| `KillPendingEntities_NoObserverBindings_NoError` | KillPending 无观察者绑定的实体 | 正常完成，实体数变为 0 |
| `ClearAll_NoObserverBindings_NoError` | RemoveAllEntities 无观察者绑定的实体 | 正常完成，实体数变为 0 |

## StrategyPriorityTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Pool_GetPriority_ReturnsExplicitPriorityFromAttribute` | Priority=100 属性正确解析 | snd-entity-model: 优先级 |
| `Pool_GetPriority_ReturnsDefault6205WhenNotSpecified` | 未指定优先级时返回默认值 6205 | snd-entity-model: 优先级 |
| `Add_DifferentPriorities_SortedAscending` | 不同 priority 按升序排列 | snd-entity-model: 策略执行顺序 |
| `Add_SamePriority_MaintainsInsertionFifoOrder` | 同 priority 保持 FIFO 插入序 | snd-entity-model |
| `Add_MixedPriorities_SortedAscWithStableFifoInSamePriority` | 混合优先级排正确，同优先保持插入序 | snd-entity-model |
| `Add_InsertBetweenExisting_PositionsCorrectly` | 中间插入排到正确位置 | snd-entity-model |
| `Process_ExecutesInPriorityAscendingOrder` | Process 按优先级升序执行 | snd-entity-model |
| `Process_SamePriority_ExecutesInInsertionOrder` | 同优先级按插入序执行 | snd-entity-model |
| `Spawn_DifferentPriorities_SortedAscending` | Spawn 时按优先级排序 | snd-entity-model |
| `Spawn_SamePriority_MaintainsInputOrder` | Spawn 同优先级保持输入序 | snd-entity-model |
| `Load_DifferentPriorities_ResortedAscending` | Load 恢复时重排为升序 | snd-entity-model |
| `SerializeIndices_ReturnsIndicesInPriorityOrder` | 序列化索引按优先级排列 | snd-entity-model |
| `SaveLoadRoundtrip_MaintainsProcessingOrder` | 序列化→恢复后 Process 顺序一致 | snd-entity-model |
| `AfterSpawn_ExecutesInPriorityAscendingOrder` | AfterSpawn 钩子按优先级 | snd-entity-model |
| `BeforeQuit_ExecutesInPriorityAscendingOrder` | BeforeQuit 钩子按优先级 | snd-entity-model |
| `AfterLoad_ExecutesInPriorityAscendingOrder` | AfterLoad 钩子按优先级 | snd-entity-model |
| `Remove_Middle_RemainingOrderPreserved` | 删除中间策略，余下顺序不变 | — |
| `Remove_First_RemainingOrderPreserved` | 删除首个策略，余下顺序不变 | — |
| `Remove_Last_RemainingOrderPreserved` | 删除末个策略，余下顺序不变 | — |
| `AddAfterRemove_InsertsAtCorrectPosition` | 删除后重新插入到正确位置 | — |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Pool_GetPriority_ReturnsZeroForUnknownIndex` | 未知索引查优先级 | 返回 0 |
| `EmptyList_ProcessDoesNotThrow` | 空策略列表 Process | 不抛异常 |
| `EmptyList_SerializeIndicesReturnsEmpty` | 空策略列表序列化 | 返回空 |
| `SingleStrategy_Works` | 单策略 | 正常工作 |
| `NegativePriorities_SortedCorrectly` | 负优先级正确排序（-10, -5, 0, 50） | — |
| `IntMinAndIntMaxPriority_SortedCorrectly` | int.MinValue 和 int.MaxValue 排序 | — |
| `DescendingPriorityInsertion_SortedAscending` | 降序插入自动升序排列 | — |
| `AscendingPriorityInsertion_SortedAscending` | 升序插入保持升序 | — |
| `AlternatingPriorityInsertion_SortedCorrectly` | 交替优先级插入后排升序 | — |
| `Remove_NonexistentStrategy_Throws` | 删除不存在的策略抛异常，已挂载策略不受影响 | — |
| `AllDefaultPriority6205_MaintainsInsertionOrder` | 全部默认优先级 6205 保持插入序 | snd-entity-model |

## StrategyPoolTypeSafetyAndExtensionTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GetStrategy_WrongBranchGeneric_DoesNotLeakReferenceCount` | 泛型类型不匹配失败后引用计数不泄漏（再次获取不是同一实例） | Strategy README: SndStrategyPool |
| `StackStateMachine_WhenSecondAcquireFails_ReleasesFirstAcquire` | StackStateMachine 构造时第一次获取成功但第二次失败，回滚第一次获取 | Strategy README: SndStrategyPool |
| `RecoverStrategiesOnly_WithOnlyValidStrategies_Succeeds` | 仅含 LifecycleStrategy 的索引列表恢复成功 | Strategy README: SndStrategyManager |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `GetStrategy_WrongBranchGeneric_ThrowsInvalidOperation` | 用 LifecycleStrategyBase 泛型获取 Active/StateMachine 策略 | InvalidOperationException |
| `RecoverStrategiesOnly_WithNonLifecycleStrategy_Throws` | Recover 列表含 ActiveStrategyBase 类型 | InvalidOperationException（"LifecycleStrategyBase"） |
| `Register_AbstractStrategyType_Throws` | 注册抽象策略类型 | InvalidOperationException |
| `Register_DuplicateIndex_Throws` | 重复注册同一策略索引 | InvalidOperationException（"already registered"） |
| `GetStrategy_FactoryReturnsNull_ThrowsInvalidOperation` | 注册工厂返回 null | InvalidOperationException（消息含 "returned null"，不得退化为 NRE） |

## SndStrategyPerformanceTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `StrategyPool_GetRelease_Throughput` | 100,000 次 Get+Release 往返吞吐与分配量在可接受范围内（< 500MB） | — |
| `StrategyManager_Process_StrategyCountScaling` | 1/5/10/20 策略 × 10,000 帧 Process 的吞吐与分配：断言 ProcessAll 后实体仍存活 | — |
| `TriggerAll_AfterSpawn_AllocationByStrategyCount` | 1/10 策略 AfterSpawn TriggerAll 的 ToArray 分配量：断言 AfterSpawn 后实体名称正确 | — |

## SndStrategyPoolLeakDetectionTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `LogPoolLeaks_AllReleased_ProducesNoWarnings` | Get 后 Release，引用计数归零，LogPoolLeaks 无 Warning | Strategy README: LogPoolLeaks |
| `LogPoolLeaks_NoStrategiesRegistered_ProducesNoWarnings` | 空池调用 LogPoolLeaks 无输出 | Strategy README: LogPoolLeaks |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `LogPoolLeaks_UnreleasedStrategy_LogsWarning` | Get 后不 Release，引用计数非零 | 输出含策略索引与 refCount 的 Warning |
| `LogPoolLeaks_MultipleLeaks_LogsWarningForEach` | 多个策略均未释放 | 每个泄漏策略各输出一条 Warning |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `SP50 / SP100 / SP200` | StrategyPriorityTests.cs | 不同 Priority 的策略（50/100/200），Process 中记录执行日志 |
| `S5 / S10A / S10B / S10C / S15 / S20 / S25 / S30 / S40 / S60 / S80 / S10` | StrategyPriorityTests.cs | 覆盖全范围优先级的策略组（5 ~ 80），部分重写 Process 记录日志 |
| `SDemo` | StrategyPriorityTests.cs | 无显式 Priority 属性的策略（默认 6205） |
| `SA / SB / SC` | StrategyPriorityTests.cs | 同默认优先级（6205）的三策略，观察 FIFO 插入序 |
| `SN10 / SN5 / SN0` | StrategyPriorityTests.cs | 负优先级策略（-10/-5/0） |
| `S0 / SMin / SMax` | StrategyPriorityTests.cs | int.Zero / int.MinValue / int.MaxValue 优先级策略 |
| `LC10 / LC20 / LC30` | StrategyPriorityTests.cs | 重写 AfterSpawn 钩子（Priority=10/20/30），验证生命周期钩子优先级 |
| `Q10 / Q20 / Q30` | StrategyPriorityTests.cs | 重写 BeforeQuit 钩子（Priority=10/20/30） |
| `LD10 / LD20 / LD30` | StrategyPriorityTests.cs | 重写 AfterLoad 钩子（Priority=10/20/30） |
| `Rec`（AsyncLocal 记录器） | StrategyPriorityTests.cs | 执行顺序日志收集器，BeginTest/Add/Reset/Log，AsyncLocal 隔离并行测试 |
| `TestLifecycleStrategy` | LifecycleStrategyBaseTests.cs | 不重写任何钩子的空白策略，验证默认实现不修改实体数据 |
| `TestLifecycleStrategyWithAdd` | LifecycleStrategyBaseTests.cs | Process 中调用 entity.AddStrategy 的场景策略 |
| `TestLifecycleStrategyKillSelf` | LifecycleStrategyBaseTests.cs | Process 中调用 RequestKillEntity(self) |
| `TestLifecycleStrategyKillOther` | LifecycleStrategyBaseTests.cs | Process 中调用 RequestKillEntity("B") 的目标实体 |
| `KillSelfRecordingStrategy` | LifecycleStrategyBaseTests.cs | Process 中 Kill 自身并记录执行日志，AsyncLocal 隔离 |
| `ProcessCalledStrategy` | LifecycleStrategyBaseTests.cs | 标记 Process 是否被调用（AsyncLocal bool），验证 Kill 后后续策略执行 |
| `ThrowOnAddStrategy` | LifecycleStrategyBaseTests.cs | AfterAdd 钩子抛出 InvalidOperationException，验证回滚并确认 Process 不执行 |
| `DuplicateAddTestStrategy` | LifecycleStrategyBaseTests.cs | 空白 LifecycleStrategy 策略，验证同一索引重复 AddStrategy 被拒绝 |
| `QueryHpStrategy` | ActiveStrategyTests.cs | ActiveStrategy：返回 100（int）或实体名（input="get_name"） |
| `CmdDamageStrategy` | ActiveStrategyTests.cs | ActiveStrategy：input 为 int 时返回 "dealt {n} damage" |
| `EntityOnlyStrategy` | ActiveStrategyTests.cs | LifecycleStrategy 占位符，用于区分 Entity/Active 类型 |
| `StatefulActiveStrategy` | ActiveStrategyTests.cs | 有实例字段 _counter 的 ActiveStrategy，验证注册时被拒绝 |
| `StatelessActiveStrategy` | ActiveStrategyTests.cs | 无状态的 ActiveStrategy，验证自动发现 |
| `UnannotatedActiveStrategy` | ActiveStrategyTests.cs | 无 [StrategyIndex] 属性的 ActiveStrategy，验证注册拒绝 |
| `SelfWatchObserver` | ObserverStrategyTests.cs | 观察 character.hp 的观察者，AsyncLocal List\<DataCall\> 记录每次 OnDataChanged 参数 |
| `MultiKeyObserver` | ObserverStrategyTests.cs | 观察 character.hp + character.mp 双键的观察者，分别记录到不同列表 |
| `NoDataKeyObserver` | ObserverStrategyTests.cs | 无 [ObserveData] 属性的观察者，验证可挂载/卸载 |
| `MemoryObserver` | ObserverStrategyTests.cs | 记录 OnMounted/OnUnmounted 调用（MountCall 含 Entity + Target），AsyncLocal 列表隔离 |
| `ThrowOnMountObserver` | ObserverStrategyTests.cs | OnMounted 抛出 InvalidOperationException，验证回滚并确认后续 SetData 不触发 |
| `ThrowOnUnmountObserver` | ObserverStrategyTests.cs | OnUnmounted 抛出 InvalidOperationException，验证失败卸载仍归还池引用 |
| `StatefulObserver` | ObserverStrategyTests.cs | 有实例字段 _counter 的观察者，验证注册时被拒绝 |
| `UnannotatedObserver` | ObserverStrategyTests.cs | 无 [StrategyIndex] 属性的观察者，验证注册拒绝 |
| `ExtensionDomainStrategyBase`（abstract） | StrategyPoolTypeSafetyAndExtensionTests.cs | 在 LifecycleStrategyBase 之上扩展的第三领域抽象根基类，定义 ProbeValue() 抽象方法 |
| `ExtensionDomainConcreteStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | ExtensionDomainStrategyBase 的具体实现，ProbeValue() 返回 "ok" |
| `PoolEntityStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | LifecycleStrategyBase 空实现，用于泛型分支安全测试 |
| `PoolStateMachineStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | StateMachineStrategyBase 空实现，用于 StackStateMachine 测试 |
| `PoolActiveStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | ActiveStrategyBase 空实现，用于 RecoverStrategiesOnly 拒绝测试 |
| `PerfPoolStrategy` | SndStrategyPerformanceTests.cs | LifecycleStrategyBase 空实现，用于策略池 Get/Release 性能测量 |
| `PerfProcessBase`（abstract） | SndStrategyPerformanceTests.cs | Process 方法为空的抽象 LifecycleStrategy，1~20 号性能策略均继承此基类 |
| `PerfProcess1Strategy` ~ `PerfProcess20Strategy` | SndStrategyPerformanceTests.cs | 20 个同名 Process 空实现策略，用于 Process 策略数缩放和 TriggerAll 分配测量 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Process 中 RequestKill 对同实体 ActiveStrategy 的影响 | 仅测试了 LifecycleStrategy 的 Kill 后剩余策略执行，未验证 ActiveStrategy 场景 | snd-entity-model |
| ActiveStrategy 的 AfterSpawn/BeforeQuit/AfterLoad 生命周期行为 | ActiveStrategy 仅测试了 Invoke + 注册 + Spawn/Load 恢复，未覆盖其是否响应非 Invoke 生命周期钩子 | Strategy README: 策略继承体系 |
| ObserverStrategy 的 BeforeDead/BeforeSave 钩子集成 | 观察者仅测试 Dead/Quit 释放路径，未验证 BeforeDead/BeforeSave 钩子中观察者的行为 | Strategy README: 策略生命周期钩子（顺序） |
| 策略池在并发 Get/Release 下的线程安全性 | 当前所有测试为单线程，未覆盖多线程场景中引用计数和池化正确性 | Strategy README: SndStrategyPool |
| 跨实体观察者的 Save/Recover 全链路（含 resolveTarget 通过 SessionManager.FindByName） | 仅测试了自观察的 Save/Recover，跨实体场景依赖 SessionManager 查找目标的路径未覆盖 | Strategy README: ObserverTopology |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
