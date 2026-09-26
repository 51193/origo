<!-- docsync-pair: Origo.Core.Tests/Snd-Strategy -->
<!-- docsync-revision: 17 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# SND 策略 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Snd/Strategy](../Origo.Core/Snd/Strategy/README.zh.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.zh.md)

## 被测行为概览

验证 SND 策略系统的全部行为：策略偏序排序、池引用计数/回收、实体策略的 8 个生命周期钩子、主动策略的 Invoke 调用、观察者策略的挂载/卸载/数据变更通知/持久化/拓扑查询、策略注册时的类型安全校验。

`SndStrategyPerformanceTests` 中的三个性能测试使用 `Stopwatch` + `PerfReporter` 测量吞吐/分配并附带正确性断言，标记 `[Trait("Category","Benchmark")]`，由 `scripts/benchmark.sh` 执行，不进入常规功能测试管线。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `ActiveStrategyTests.cs` | 主动策略 Invoke 调用、Spawn/Load 恢复、Quit/Dead 释放、动态增删、序列化、注册校验、Entity/Active 混合场景 |
| `ActiveStrategyJsonBaseTests.cs` | ActiveStrategyJsonBase JSON 契约：输入反序列化/结果序列化、错误输入返回 err 结果、裸字符串结果直通、null 输入执行、泛型扩展调用往返 |
| `LifecycleStrategyBaseTests.cs` | 默认钩子不变更数据；Process 中 Add/Kill/SelfKill/OtherKill 的并发语义；AfterAdd 失败回滚；不存在策略操作的安全处理 |
| `ObserverStrategyTests.cs` | 观察者注册/无状态校验；Mount/Unmount 生命周期与参数正确性；数据变更通知（正确键/非观察键/卸载后）及新旧值；多键观察；序列化（ObserverIndices 填充/空绑定/分组）；Dead/Quit 释放与 OnUnmounted；属性反射提取；跨实体挂载拒绝；null/空/未知参数防御；RecoverBindings 容错；Has/Remove 拓扑查询；Teardown/KillPending/ClearAll 清理路径 |
| `StrategyOrderingTests.cs` | 完整注册图投影、动态增删、保存/加载/退出/死亡、启动固定、错误声明和环诊断 |
| `StrategyOrderingIntegrationTests.cs` | 真实模拟宿主：Ordinal 排序、BeforeRemove 重入插入、启动后拒绝注册；均已验证红 → 绿 |
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

## StrategyOrderingTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `FullRegistry_ProjectsTransitiveOrder_RegardlessOfRegistrationAndMountingOrder` | 注册与挂载顺序均不影响结果；仅挂载 A/C 时仍保留 A → B → C 的传递关系；AfterSpawn、Process 与元数据使用同一顺序 | snd-entity-model: 策略执行顺序 |
| `DynamicAddAndRemove_KeepTransitiveOrderAndOptionalTargets` | 动态增删按完整注册关系插入和重排；目标策略未挂载仍保留约束；重复添加与移除未挂载索引抛异常 | snd-entity-model: 策略执行顺序 |
| `SaveLoadQuitAndDead_AllUseSameProjectedOrder` | 存档、恢复、退出和死亡全部使用同一投影顺序；恢复后的元数据顺序正确，退出后无池引用泄漏 | snd-entity-model: 策略执行顺序 |
| `EquivalentBeforeAfterAndDuplicateEdges_DoNotCreateFalseCycles` | Before / After 等价边与重复声明去重，不产生假环；未知索引查询排序抛异常；固定后注册被拒绝 | Strategy README: SndStrategyPool |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `BootstrapWithoutAutoDiscovery_ValidatesUnusedConstraintsImmediately` | 未被挂载的策略声明了未注册目标 | 启动固定注册表时抛 InvalidOperationException |
| `UnknownTarget_FailsBeforeAnyEntityOrPoolReferenceIsCreated` | Before 指向未注册索引 | SealRegistration 抛 InvalidOperationException，消息含目标索引与 unregistered |
| `NonLifecycleTarget_IsRejected` | 生命周期策略引用非生命周期目标 | SealRegistration 抛 InvalidOperationException，消息含 non-lifecycle |
| `Cycles_FailWithAnActualClosedPath_WithoutIncludingUnrelatedPredecessors` | 生命周期顺序声明成环且存在无关前驱 | 抛 InvalidOperationException，消息含实际闭合路径且不含无关前驱；重复 Seal 仍抛异常 |
| `Cycles_WithAcyclicBranch_ReportOnlyClosedPath` | 环外存在已完成遍历的无关无环节点 | 抛 InvalidOperationException，消息只含闭合路径，不含无环节点 |
| `InvalidDeclarations_FailAtRegistration` | 自引用、空白目标、null 数组，或非生命周期策略声明约束 | 注册时立即抛 InvalidOperationException |
| `DirectEntityRecovery_SealsRegistrationWithoutBootstrap` | 直接恢复生命周期策略后再次注册 | 恢复触发固定注册表，后续 Register 抛 InvalidOperationException |

## StrategyOrderingIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SpawnAndProcess_UnconstrainedStrategies_UseOrdinalIndexOrder` | 未声明约束的生命周期策略按索引 Ordinal 顺序执行，与挂载顺序无关 | snd-entity-model: 策略执行顺序 |
| `RemoveStrategy_HookChangesOtherEntries_RemovesRequestedEntry` | BeforeRemove 内移除其他策略并新增策略后，按条目身份摘除目标策略，不误删其他条目、不重复释放池引用 | Strategy README: 为什么固定完整注册图 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `RegisterStrategy_AfterFirstLifecycleEntity_FailsExplicitly` | 生命周期实体启动后注册新策略 | 抛 InvalidOperationException，注册表保持固定 |

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
| `RecoverStrategiesOnly_DuplicateIndex_ThrowsBeforeAcquiring` | Recover 列表含重复生命周期索引 | InvalidOperationException（"more than once"），未获取任何策略引用 |
| `Recover_DuplicateActiveIndex_ThrowsBeforeAcquiring` | Recover 列表含重复主动策略索引 | InvalidOperationException（"more than once"），未获取或泄漏策略引用 |
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
| `Probe`（abstract） | StrategyOrderingTests.cs | 记录 8 个生命周期钩子事件的抽象基类，供 Producer / Bridge / Consumer 复用 |
| `Producer` / `Bridge` / `Consumer` | StrategyOrderingTests.cs | 以重复 Before、等价 After 和未挂载中间策略验证 A → B → C 投影、去重与无约束 Ordinal 兜底 |
| `MissingTarget` | StrategyOrderingTests.cs | Before 指向未注册索引，验证 SealRegistration 的未知目标错误 |
| `ActiveTarget` | StrategyOrderingTests.cs | ActiveStrategy 目标，验证生命周期约束引用非生命周期策略被拒绝 |
| `ReferencesActive` | StrategyOrderingTests.cs | 生命周期策略引用 ActiveTarget，验证非生命周期引用错误 |
| `OrderedActive` | StrategyOrderingTests.cs | ActiveStrategy 声明 Before 约束，验证注册时拒绝 |
| `CycleA` / `CycleB` / `CycleC` / `CycleRoot` | StrategyOrderingTests.cs | 构成真实环并附带无关前驱，验证闭合路径诊断不包含无关节点 |
| `AcyclicLeaf` | StrategyOrderingTests.cs | 无出边的无环节点，验证环检测完成该分支后只报告闭合路径 |
| `SelfReference` / `BlankReference` / `NullReference` | StrategyOrderingTests.cs | 自引用、空白目标和 null 数组声明的非法注册用例 |
| `OrderingAlpha` / `OrderingZulu` / `OrderingRemover` | StrategyOrderingIntegrationTests.cs | 真实模拟宿主中验证 Ordinal 兜底顺序，以及 BeforeRemove 重入增删时的条目身份摘除 |
| `ExtensionDomainStrategyBase`（abstract） | StrategyPoolTypeSafetyAndExtensionTests.cs | 在 LifecycleStrategyBase 之上扩展的第三领域抽象根基类，定义 ProbeValue() 抽象方法 |
| `ExtensionDomainConcreteStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | ExtensionDomainStrategyBase 的具体实现，ProbeValue() 返回 "ok" |
| `PoolEntityStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | LifecycleStrategyBase 空实现，用于泛型分支安全测试 |
| `PoolStateMachineStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | StateMachineStrategyBase 空实现，用于 StackStateMachine 测试 |
| `PoolActiveStrategy` | StrategyPoolTypeSafetyAndExtensionTests.cs | ActiveStrategyBase 空实现，用于 RecoverStrategiesOnly 拒绝测试 |
| `PerfPoolStrategy` | SndStrategyPerformanceTests.cs | LifecycleStrategyBase 空实现，用于策略池 Get/Release 性能测量 |
| `PerfProcessBase`（abstract） | SndStrategyPerformanceTests.cs | Process 方法为空的抽象 LifecycleStrategy，1~20 号性能策略均继承此基类 |
| `PerfProcess1Strategy` ~ `PerfProcess20Strategy` | SndStrategyPerformanceTests.cs | 20 个同名 Process 空实现策略，用于 Process 策略数缩放和 TriggerAll 分配测量 |

## 已知覆盖缺口

无——以下边界属于当前契约而非未覆盖缺口：

- `ActiveStrategyBase` 与 `ObserverStrategyBase` 是和 `LifecycleStrategyBase` 并列的策略分支，刻意不参与帧更新与被动策略的 8 个生命周期钩子；ActiveStrategy 的 Invoke/恢复/释放路径由本文档覆盖，ObserverStrategy 的生命周期顺序由 [Integration.zh.md](Testing/Integration/Integration.zh.md) 覆盖。
- `SndStrategyPool` 的引用计数只承诺单线程帧模型，并发访问不是当前契约。
- 跨实体观察者的 Save/Recover 全链路（含目标缺失时的会话整体回滚）由 `ObserverTopologyIntegrationTests` 与 `SessionRunLoadRollbackMaskingTests` 覆盖。

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
