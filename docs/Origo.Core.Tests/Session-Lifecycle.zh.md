<!-- docsync-pair: Origo.Core.Tests/Session-Lifecycle -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 会话生命周期 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Runtime/Lifecycle](../Origo.Core/Runtime/Lifecycle/README.zh.md)
> [↔ 被测行为: usage/session-model](../usage/session-model.zh.md)

## 被测行为概览

验证 Origo 会话模型的完整生命周期：SessionRun/ProgressRun 创建与销毁、Dispose 语义（幂等、不自动持久化、BeforeQuit 钩子、异常安全）、
前后台会话的接口与行为一致性（ISessionRun）、IsFrontSession 标志、前台唯一性约束、会话拓扑编解码、
关卡切换（SwitchForeground）、保存→切换→读取往返、会话间解耦（独立黑板/SceneHost）、
SessionManager 完整 API（创建/查找/销毁/枚举/ProcessAll/KillPending）、以及 ProgressRun 与后台会话的持久化往返。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `LifecycleRunsTests.cs` | SessionRun/ProgressRun 生命周期、MountKey、LoadFromPayload、SwitchForeground、日志 |
| `DisposeSemanticsTests.cs` (3 partial files: `SessionRun.cs`, `ProgressRun.cs`, `RoundTrip.cs`) | Dispose 不触发 BeforeSave、触发 BeforeQuit、幂等、Save-then-Dispose-then-Continue 往返、异常安全 |
| `ForegroundBackgroundContractTests.cs` | 前后台 ISessionRun 行为完全一致（黑板/状态机/序列化/Dispose） |
| `EmptySessionManagerTests.cs` | EmptySessionManager 的无操作行为 |
| `PlayStopPlayRoundTripTests.cs` | 多次 Play→Stop→Play 的往返一致性（身份/黑板/Tick/Progress） |
| `ProgressRunSessionLoadingEdgeTests.cs` | ProgressRun 加载错误路径（拓扑格式错误/文件缺失/后台加载失败） |
| `SaveAndSwitchForegroundIntegrationTests.cs` | 保存+切换关卡的组合操作、碰撞处理、延迟队列编排 |
| `SessionDecouplingTests.cs` | 会话独立运行互不干扰（SessionStateMachineContext、SceneHost、路径策略） |
| `SessionManagerTests.cs` | ISessionManager：创建/查找/销毁/枚举/ProcessAll/KillPending |
| `SessionTopologyCodecTests.cs` | SessionTopology 编解码往返 |
| `TopologyInvariantTests.cs` | 拓扑不变量校验：EnsureActiveLevel 对有效/缺失/空/空白/不匹配拓扑的验证（fail-fast） |
| `BackgroundSessionTests.cs` | 后台会话独立测试（实体/Process/序列化/持久化往返） |
| `BackgroundSession_CreationWithCorrectFlagTests.cs` | 后台 IsFrontSession=false |
| `BackgroundSession_MultipleInstancesAllowedTests.cs` | 后台可多实例 |
| `FrontSession_CreationWithCorrectFlagTests.cs` | 前台 IsFrontSession=true |
| `FrontSession_StrategyContextReceivesFrontFlagTests.cs` | 策略上下文接收前台标志 |
| `FrontSession_UniqueConstraintValidationTests.cs` | 前台唯一性约束 |

## LifecycleRunsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ProgressRun_LoadFromPayload_RestoresProgressAndSession` | 从 Payload 恢复 Progress 和 Session 黑板数据 | session-model: 持久化恢复 |
| `ProgressRun_SwitchForegroundLevel_PersistsOldSession_AndLoadsNewSessionFromCurrent` | 切换关卡时旧会话显式持久化、新会话从 current/ 加载 | session-model: 关卡切换 |
| `ProgressRun_SwitchForegroundLevel_WhenTargetMissing_EntersEmptySessionAndClearsScene` | 目标关卡无数据时进入空会话、清空 Scene | session-model |
| `ProgressRun_LoadAndMountForeground_SyncsSessionTopologyToProgressBlackboard` | LoadAndMountForeground 后拓扑写入 ProgressBlackboard | session-model: 会话拓扑 |
| `SessionRun_SerializeToPayload_RoundTrip_PreservesBlackboardData` | RequestSaveGame 写入文件后黑板数据不丢失 | session-model: 持久化 |
| `LoadAndMountForeground_WhenNoPayloadFound_MountsEmptySession` | 无存档数据时加载空会话 | session-model |
| `SessionRun_Create_LogsCreation` | 创建 SessionRun 时记录日志 | Logging |
| `ProgressRun_Create_LogsCreation` | 创建 ProgressRun 时记录日志 | Logging |
| `SessionManager_Mount_LogsMounting` | 挂载会话时记录日志 | Logging |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SessionRun_Dispose_ClearsSessionAndScene_ThenThrowsOnAccess` | Dispose 后访问 SessionBlackboard/FindByName/StateMachines | ObjectDisposedException |
| `ProgressRun_LoadFromPayload_WithEmptyProgressNode_ThrowsMissingSessionTopology` | progress.json 无拓扑字段 | InvalidOperationException |
| `ProgressRun_BuildSavePayload_ThrowsWhenProgressTopologyForegroundDoesNotMatchForeground` | 拓扑中的前台与实际前台不匹配 | InvalidOperationException |
| `SessionRun_LoadFromPayload_WhenSceneLoadFails_ResetsSessionState` | Scene JSON 语法错误 | Exception |
| `ProgressRun_LoadFromPayload_MissingProgressStateMachinesNode_Throws` | Payload 缺 ProgressStateMachinesNode | InvalidOperationException |
| `LoadAndMountForeground_WithEmptyLevelId_Throws` | 空或空白 levelId | ArgumentException |
| `SwitchForeground_WithEmptyLevelId_Throws` | 空或空白 levelId | ArgumentException |
| `BuildSavePayload_WithoutTopologySet_Throws` | ProgressBlackboard 中拓扑为空字符串 | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SessionRun_MountKey_IsNull_WhenNotMounted` | 创建后台后销毁，Contains 返回 false | 已卸载 |
| `SessionRun_MountKey_SetOnMount_ClearedOnUnmount` | 创建后台后 Contains 为 true，销毁后为 false | ISessionManager 正确管理挂载 |
| `SessionRun_Dispose_AutoUnmountsFromManager` | Dispose 时自动从 Manager 卸载 | Contains 返回 false |
| `SessionManager_Clear_EmptiesAllSessions` | 逐个 DestroySession 后 Keys 为空 | 全部清空 |
| `ResolveLevelPayload_ReturnsNull_WhenNoData` | 不存在存档时返回 null | 返回 null |

## DisposeSemanticsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SessionRun_Dispose_DoesNotWriteFilesToCurrent` | Dispose 不自动写入文件（通过 ISaveStorageService 检查） | session-model: Dispose 不持久化 |
| `SessionRun_Dispose_DoesNotTriggerBeforeSave` | Dispose 不触发 BeforeSave 钩子 | session-model: Dispose 不持久化 |
| `SessionRun_Dispose_TriggersBeforeQuit` | Dispose 触发 BeforeQuit 钩子 | session-model |
| `SessionRun_ExplicitPersistLevelState_WritesToCurrent_BeforeDispose` | 通过 RequestSaveGame 持久化写入文件 | session-model |
| `SessionRun_ExplicitPersistLevelState_TriggersBeforeSave` | 通过 RequestSaveGame 触发 BeforeSave | session-model |
| `ProgressRun_Dispose_DoesNotCallPersistProgress` | ProgressRun.Dispose 不调用 PersistProgress | session-model |
| `ProgressRun_Dispose_DeletesCurrentDirectory` | ProgressRun.Dispose 删除 current/ | session-model |
| `SessionRun_AfterDispose_SaveDoesNotPersistSessionData` | Dispose 后保存不包含已释放会话的数据 | 文件不存在 |
| `SessionRun_AfterDispose_SaveExcludesDisposedSession` | Dispose 后 RequestSaveGame 排除已释放会话 | 文件不存在 |
| `ExplicitSave_ThenDispose_ThenContinue_LoadsSavedState` | 显式保存→Dispose→Continue 往返恢复实体与黑板 | session-model: 持久化 |
| `Save_ThenDispose_ThenContinue_ProgressBlackboardPreserved` | ProgressBlackboard 数据在 Continue 后保留 | session-model |
| `SaveAfterSwitch_HasCorrectActiveLevel` | 切换后保存的 ActiveLevelId 正确 | session-model |
| `SaveSwitchDisposeReload_RestoresToSavedState` | Save→Switch→Dispose→ReLoad 完整往返恢复全部状态 | session-model |
| `FullRoundTrip_SwitchForeground_OldLevelDataPersistedImplicitly` | SwitchForeground 时旧前台关卡数据被显式持久化到 current/ | session-model |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SessionRun_AfterDispose_SessionBlackboard_ThrowsObjectDisposed` | Dispose 后访问 SessionBlackboard | ObjectDisposedException |
| `SessionRun_AfterDispose_SceneHost_ThrowsObjectDisposed` | Dispose 后 FindByName | ObjectDisposedException |
| `SessionRun_AfterDispose_GetSessionStateMachines_ThrowsObjectDisposed` | Dispose 后获取状态机 | ObjectDisposedException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SessionRun_Dispose_Twice_IsIdempotent` | 两次 Dispose 不抛异常 | 幂等 |
| `ProgressRun_Dispose_Twice_IsIdempotent` | 两次 Dispose 不抛异常 | 幂等 |
| `ProgressRun_Dispose_DeletesCurrentDirectory_EvenWhenEmpty` | 空 current/ 时 Dispose 安全 | 幂等 |
| `ProgressRun_AfterDispose_ForegroundSession_IsNull` | Dispose 后 ForegroundSession 为 null | 返回 null |
| `ProgressRun_AfterDispose_SessionManagerKeys_IsEmpty` | Dispose 后 Keys 为空 | 空集合 |
| `ProgressRun_AfterDispose_ProgressBlackboard_IsCleared` | Dispose 后 ProgressBlackboard 清空 | TryGet 返回 false |
| `ProgressRun_Dispose_SafeEvenWhenNoCurrentDirectory` | 无 current/ 时 Dispose 安全 | 不抛异常 |
| `ProgressRun_Dispose_StateMachineContainerClear_DoesNotThrow` | 状态机容器 Clear 不抛异常 | 不抛异常 |
| `SessionRun_Dispose_BeforeQuit_CanAccessSceneHost` | BeforeQuit 期间会话资源仍可访问 | 不抛 ObjectDisposedException |
| `SessionRun_Dispose_BeforeQuitThrows_EntitiesStillRemoved` | BeforeQuit 抛异常后实体仍被移除 | 异常传播但实体已清理 |
| `SessionRun_Dispose_BeforeQuitThrows_DoubleDisposeStillIdempotent` | BeforeQuit 抛异常后再次 Dispose 不抛 | 幂等 |

## ForegroundBackgroundContractTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateBackgroundSession_ReturnsISessionRun_NotConcreteType` | 后台会话作为 ISessionRun 暴露 | session-model: 前后台共享接口 |
| `CreateBackgroundSession_ThenLoadPayload_ReturnsISessionRun_NotConcreteType` | 后台会话加载 Payload 后仍作为 ISessionRun 暴露 | session-model: 前后台共享接口 |
| `ForegroundSession_ExposedAsISessionRun` | 前台会话作为 ISessionRun 暴露 | session-model |
| `SerializeToPayload_ProducesSameFormat_ForForegroundAndBackground` | 序列化格式一致 | session-model |
| `LoadFromPayload_WorksIdentically_ForForegroundAndBackground` | 反序列化行为一致 | session-model |
| `SessionBlackboard_ReadWrite_IdenticalBehavior` | 黑板读写行为一致 | session-model |
| `SessionBlackboard_Isolated_BetweenForegroundAndBackground` | 前后台黑板数据隔离 | session-model |
| `StateMachines_WorkIdentically_ForForegroundAndBackground` | 前后台状态机触发策略钩子行为一致 | session-model |
| `PersistLevelState_WritesToStorage_ForBothForegroundAndBackground` | 前后台 PersistLevelState 行为一致 | session-model |
| `BusinessCode_CanTreatBothSessionsIdentically_ThroughInterface` | 业务代码可统一通过 ISessionRun 操作前后台 | session-model |
| `RoundTrip_SerializeAndLoad_IdenticalBetweenForegroundAndBackground` | 前后台序列化往返一致 | session-model |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Dispose_ThrowsOnAccess_ForBothForegroundAndBackground` | Dispose 后访问 SessionBlackboard/FindByName/StateMachines | ObjectDisposedException |

## EmptySessionManagerTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `EmptySessionManager_CreateBackgroundSession_Throws` | 调用 CreateBackgroundSession | InvalidOperationException（包含 "ProgressRun"） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `EmptySessionManager_QueryAndNoOps` | ForegroundSession 为 null、Keys 为空、TryGet/Contains 返回 null/false、Destroy/ProcessAll 无操作 | 全部无操作且不抛异常 |

## PlayStopPlayRoundTripTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RoundTrip_ForegroundIdentity_Preserved` | 序列化→Dispose→重建→反序列化后 IsFrontSession 和 LevelId 保留 | session-model |
| `RoundTrip_BackgroundTickState_Preserved` | 后台 syncProcess 标志在往返后正确恢复 | session-model |
| `RoundTrip_SessionBlackboards_Isolated_NoCrossContamination` | 前后台黑板数据在往返后仍隔离 | session-model |
| `RoundTrip_ProgressBlackboard_Shared_AcrossSessions` | ProgressBlackboard 数据在往返后可跨会话共享 | session-model |
| `RoundTrip_AllSessionProperties_Restored_Correctly` | 所有会话属性（标志/LevelId/黑板/Tick(syncProcess)）在往返后正确恢复 | session-model |
| `LoadFromPayload_FullyRestoresFromPayloadOnly` | LoadFromPayload 完全从 Payload 恢复，不从外部黑板注入 | session-model |
| `PayloadCodec_InMemoryRoundTrip_PreservesState` | 隔离的内存 payload 往返（BuildSavePayload→LoadFromPayload，不经磁盘）保留前后台状态 | session-model |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `NewProgressRun_AlwaysStartsWithEmptyBlackboard` | ProgressRun 始终自建空白 ProgressBlackboard | ForegroundSession 为 null、Keys 为空、黑板键为空 |
| `LoadFromPayload_CanBeCalledMultipleTimes` | 对已加载的 ProgressRun 再次调用 LoadFromPayload | 干净替换全部状态，无上次加载残留 |

## ProgressRunSessionLoadingEdgeTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `LoadFromPayload_WhenTopologyMalformed_ThrowsInvalidOperation` | 拓扑条目格式错误（如 `bad_entry`） | InvalidOperationException（包含 "Malformed session topology entry"） |
| `LoadFromPayload_WhenTopologyMissing_ThrowsInvalidOperation` | progress.json 无拓扑字段 | InvalidOperationException |
| `LoadAndMountForeground_WhenSndSceneIsEmpty_ThrowsInvalidOperation` | snd_scene.json 为空或空白 | InvalidOperationException（包含 "invalid snd_scene.json"） |
| `LoadAndMountForeground_WhenSessionStateMachineJsonIsMalformed_Throws` | session_state_machines.json 语法错误 | Exception |
| `LoadFromPayload_WhenBackgroundSessionLoadFails_ClearsMountedSessions` | 后台会话 snd_scene 格式无效致加载失败 | 前台置 null、不含后台 key |

## SaveAndSwitchForegroundIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `FullMemorySndSceneHost_Spawn_FindByName_FindsSelfDuringAfterSpawn` | AfterSpawn 钩子中 FindByName 能查到自己 | session-model: FindByName |
| `FullMemorySndSceneHost_Spawn_FindByName_FindsSiblingsDuringAfterSpawn` | AfterSpawn 钩子中 FindByName 能查到兄弟实体 | session-model |
| `FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSelfDuringAfterLoad` | AfterLoad 钩子中 FindByName 能查到自己 | session-model |
| `FullMemorySndSceneHost_LoadFromMetaList_FindByName_FindsSiblingsDuringAfterLoad` | AfterLoad 钩子中 FindByName 能查到兄弟实体 | session-model |
| `SaveBackgroundWithEntities_ThenSwitchForeground_LoadsEntitiesIntoForeground` | 保存后台→销毁→切换前台，实体加载到新前台 | session-model: 关卡切换 |
| `SaveBackgroundWithEntities_ThenSwitchForeground_PreservesBlackboard` | 保存后台→切换后黑板数据保留 | session-model |
| `SaveBackgroundWithEntities_ThenSwitchForeground_LevelIdMustNotConflict` | 切换后原后台 key 不存在，前台占有关卡 | session-model |
| `PersistProgress_WritesFullTopologyIncludingBackgroundSessions` | PersistProgress 写入完整拓扑（前台+后台） | session-model: 会话拓扑 |
| `SwitchForeground_PreservesBackgroundSessionsInTopology` | 切换后拓扑中后台信息保留 | session-model |
| `SwitchForeground_WithoutBackgroundSessions_TopologyIsForegroundOnly` | 无后台时拓扑仅含前台 | session-model |
| `SwitchForeground_WithMultipleBackgroundSessions_PreservesAllInTopology` | 多后台时全部保留在拓扑中 | session-model |
| `SaveBackgroundSession_ThenSwitch_WritesAllLevelDataToCurrent` | 保存后台→切换后将关卡数据写入 current/ | session-model |
| `SaveBackgroundSession_ThenSwitch_ProgressJsonHasCorrectActiveLevel` | 切换后 ActiveLevelId 正确 | session-model |
| `SaveBackgroundSession_ThenSwitch_ThenReloadFromSnapshot_EntitiesPreserved` | 完整往返：保存→切换→snapshot→重新加载，实体保留 | session-model |
| `SwitchForeground_WithoutSave_WhenTargetLevelInBackgroundSession_LoadsEntities` | 直接切换（不显式保存）到后台持有关卡 | session-model |
| `RequestSaveGameAuto_ThenRequestSwitchForeground_EntitiesLoadRegardlessOfFlushOrder` | Deferred 队列中 Save 和 Switch 编排正确 | session-model: 关卡切换 |
| `SwitchForeground_AutoPersistsOldForegroundSessionToCurrent` | 切换时旧前台自动持久化 | session-model |
| `SwitchForeground_BackgroundSessionEntitiesUntouched` | 切换后后台实体不受影响 | session-model |
| `SwitchForeground_BackgroundSessionTickStatePreserved` | 切换后后台 syncProcess 标志保留 | session-model |
| `RequestSwitchForegroundLevel_ExecutesInSystemDeferredQueue` | Switch 在系统延迟队列中执行 | session-model |
| `RequestSwitchForegroundLevel_RunsAfterBusinessDeferred` | Switch 在业务延迟队列之后执行 | session-model |
| `SwitchForeground_ExplicitPersist_WritesOldForegroundToCurrent` | 显式 PersistForegroundLevelState 写入旧前台数据 | session-model |
| `SwitchForeground_BackgroundSessionStateIsNotAutoPersisted` | 切换时后台数据不自动持久化 | session-model |
| `SwitchForeground_BackgroundSessionStateCanBeExplicitlyPersisted` | 显式 PersistSession 可持久化后台 | session-model |
| `SwitchForeground_BackgroundCollision_AutoDestroysBackground` | 前台切换到后台持有 levelId 时自动销毁后台 | session-model |
| `SwitchForeground_BackgroundCollision_PreservesBackgroundData` | 切换碰撞时后台数据被保留并通过前台恢复 | session-model |
| `SwitchForeground_BackgroundCollision_ManyEntitiesAllPreserved` | 碰撞切换时大量实体全部保留 | session-model |
| `SwitchForeground_BackgroundCollision_OtherBackgroundsUntouched` | 碰撞切换时其他后台不受影响 | session-model |
| `SwitchForeground_BackgroundCollision_TopologyCorrectAfterAutoDestroy` | 自动销毁后拓扑正确（不含已销毁的后台） | session-model |
| `SwitchForeground_BackgroundCollision_WithForegroundActive` | 前台活跃时碰撞切换，旧前台数据不污染新前台 | session-model |
| `SwitchForeground_BackgroundCollision_ProgressPersistedAtEnd` | 碰撞切换完成后 progress.json 存在且 correct | session-model |
| `SwitchForeground_BackgroundCollision_NoDataLossRoundTrip` | 碰撞切换→保存→重新加载完整往返无数据丢失 | session-model |
| `SwitchForeground_BackgroundCollision_DeferredQueueHandling` | 延迟队列中的碰撞切换正确处理 | session-model |
| `SwitchForeground_BackgroundCollision_SubsequentSwitchStillWorks` | 连续两次碰撞切换正确 | session-model |
| `SaveBackgroundSession_ManyEntities_ThenSwitch_AllLoaded` | 50 个实体的碰撞切换全部加载 | session-model |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `BuildSavePayload_LevelIdCollision_CaughtAtSessionCreation` | 创建后台时 levelId 与前台冲突 | InvalidOperationException |
| `BuildSavePayload_WithoutForegroundSession_Throws` | 无前台会话时 BuildSavePayload | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SaveBackgroundSession_WithNoEntities_ThenSwitch_LoadsEmptyForeground` | 后台无实体切换后前台为空 | 黑板保留、实体为空 |
| `SwitchForeground_ToSameLevel_ReloadsFromCurrent` | 切换到同一关卡从 current/ 重载 | 实体和黑板数据保留 |
| `SwitchForeground_WithoutSave_WhenTargetMissing_EntersEmptySession` | 目标关卡无数据 | 进入空会话 |
| `SwitchForeground_BackgroundCollision_EmptyBackgroundWorks` | 空后台碰撞切换 | Normal |

## SessionDecouplingTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SessionStateMachineContext_Binds_SessionBlackboard` | 前后台状态机各自绑定独立 SessionBlackboard | session-model: 会话解耦 |
| `SessionStateMachineContext_Binds_SceneAccess` | 前后台状态机各自绑定独立 SceneAccess | session-model |
| `SceneHost_ReturnsISndSceneHost_ForBothForegroundAndBackground` | 前后台 SceneHost 均为 ISndSceneHost | session-model |
| `BackgroundSession_SceneHost_Spawn_FindByName_WithoutCasting` | 后台 SceneHost 无需类型转换即可 Spawn/FindByName | session-model |
| `DefaultSaveStorageService_Uses_Injected_PathPolicy` | ISaveStorageService 使用注入的 ISavePathPolicy | ISaveStorageService |
| `LevelBuilder_Commit_UsesStorageService` | LevelBuilder.Commit 委托到 ISaveStorageService | session-model |

## SessionManagerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateBackgroundSession_AddsSession_TryGetReturnsIt` | 创建后台后 TryGet 可获取同一实例 | ISessionManager |
| `DestroySession_RemovesSession_TryGetReturnsNull` | 销毁后 TryGet 返回 null | ISessionManager |
| `ForegroundKey_IsAvailable_WhenForegroundSessionExists` | 前台存在时 ForegroundSession 非 null | ISessionManager |
| `DestroySession_ForegroundKey_ClearsForegroundSession` | 销毁前台 key 后 ForegroundSession 为 null | ISessionManager |
| `TryGet_ForegroundKey_ReturnsForegroundSession` | TryGet(ForegroundKey) 返回前台会话 | ISessionManager |
| `Contains_ForegroundKey_TrueWhenSessionActive` | 前台存在时 Contains(ForegroundKey) 为 true | ISessionManager |
| `Keys_IncludesForegroundAndBackground` | Keys 包含前台和后台的键 | ISessionManager |
| `ProcessAllSessions_OnlyProcessesSyncedSessions` | ProcessAllSessions 仅处理 syncProcess=true 的会话 | ISessionManager |
| `SessionTopology_WellKnownKey_Exists` | WellKnownKeys.SessionTopology 常量存在 | 常量定义 |
| `SwitchForeground_AutoHandlesBackgroundSessionCollision` | SwitchForeground 自动处理后台 levelId 碰撞 | ISessionManager |
| `CreateForegroundSession_DifferentLevelId_Succeeds` | 不同 levelId 的后台可创建后切换前台 | ISessionManager |
| `AppendBackgroundPayloads_DifferentLevelIds_IncludesBothInPayload` | Payload 包含前台和后台不同 levelId | ISessionManager |
| `SessionRun_Spawn_CreatesEntity` | ISessionRun.Spawn 创建实体 | ISessionRun |
| `SessionRun_SpawnMany_CreatesMultipleEntities` | ISessionRun.SpawnMany 创建多个实体 | ISessionRun |
| `SessionRun_RequestKillEntity_MarksEntityPending` | RequestKillEntity 标记 IsPendingKill | ISessionRun |
| `KillPendingAllSessions_ProcessesForegroundPendingKill` | KillPendingAllSessions 执行待击杀实体的清理 | ISessionRun |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `CreateBackgroundSession_DuplicateKey_Throws` | 创建后台时 key 重复 | InvalidOperationException |
| `CreateBackgroundSession_DuplicateLevelIdWithForeground_Throws` | 后台 levelId 与前台冲突 | InvalidOperationException |
| `CreateBackgroundSession_DuplicateLevelIdWithAnotherBackground_Throws` | 后台 levelId 与另一个后台冲突 | InvalidOperationException |
| `AppendBackgroundPayloads_LevelIdCollisionBetweenForegroundAndBackground_Throws` | 后台 levelId 与前台冲突 | InvalidOperationException |
| `CreateBackgroundSession_DuplicateLevelId_ClearErrorMessage` | levelId 冲突 | InvalidOperationException（含 key/levelId/owner/Destroy 建议） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `DestroySession_NonExistentKey_DoesNotChangeMountedSessions` | 销毁不存在的 key | 不影响已挂载的会话 |
| `ForegroundSession_ReflectsProgressRunForegroundSession` | ProgressRun 存在但无前台会话时 | ForegroundSession 为 null |
| `CreateBackgroundSession_SameLevelIdAsDestroyedSession_Succeeds` | 使用已被销毁会话的 levelId | 成功创建（levelId 已释放） |

## SessionTopologyCodecTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Parse_AndSerialize_RoundTripPreservesDescriptors` | Serialize→Parse 往返保留 key/levelId/syncProcess | SessionTopologyCodec |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Parse_MalformedOrEmptyKeyOrLevel_ThrowsInvalidOperation` | 格式错误/空 key 或 levelId | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Parse_ExtraFields_ThrowsInvalidOperation` | levelId 中含 `=` 分隔符（字段数多于 3） | InvalidOperationException（必须恰好 key=levelId=syncProcess 三字段） |
| `Parse_SyncFieldParsing_FollowsBoolTryParseRules` | syncProcess 字段为 TRUE/true/False/not_bool | 按 bool.TryParse 规则解析；非布尔值抛 InvalidOperationException |

## TopologyInvariantTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `EnsureActiveLevel_ValidTopology_DoesNotThrow` | 黑板中拓扑含目标 levelId | 不抛异常，校验通过 | TopologyInvariant |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `EnsureActiveLevel_MissingTopology_Throws` | 黑板无拓扑键 | InvalidOperationException |
| `EnsureActiveLevel_EmptyTopology_Throws` | 拓扑为空字符串 | InvalidOperationException |
| `EnsureActiveLevel_WhitespaceTopology_Throws` | 拓扑为空白字符串 | InvalidOperationException |
| `EnsureActiveLevel_MismatchedLevelId_Throws` | 拓扑前台 levelId 与期望不一致 | InvalidOperationException |
| `EnsureActiveLevel_NullBlackboard_Throws` | 黑板为 null | ArgumentNullException |
| `EnsureActiveLevel_EmptyExpectedLevelId_Throws` | 期望 levelId 为空字符串 | ArgumentException |
| `Join_EmptyEntries_ReturnsEmptyString` | 空条目列表 | 返回空字符串 |
| `Parse_IgnoreEmptyEntries` | 条目间有连续逗号（空条目） | 空条目被忽略 |

## BackgroundSessionTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateBackgroundSession_ReturnsInitializedSession` | 创建后台后 LevelId/SessionBlackboard/StateMachines/SceneHost 均初始化 | ISessionManager |
| `SharedProgressBlackboard_ForegroundWriteVisibleToBackground` | 前台写入 ProgressBlackboard 对后台可见 | session-model |
| `SharedProgressBlackboard_BackgroundWriteVisibleToForeground` | 后台写入 ProgressBlackboard 对前台可见 | session-model |
| `SharedSndWorld_StrategiesFireInBackground` | 后台实体的策略钩子触发 | session-model |
| `SessionContext_OwningSession_CorrectlyBoundToBackgroundSession` | 策略 Process 中 OwningSession.LevelId 为后台会话 levelId | session-model |
| `OwnSessionBlackboard_IsolatedFromForeground` | 后台黑板数据对前台不可见 | session-model |
| `OwnEntities_IsolatedFromForeground` | 后台实体对前台不可见 | session-model |
| `KillAllEntities_FireBeforeDead` | RequestKillEntity + KillPendingAllSessions 触发全部实体 BeforeDead | ISessionRun |
| `Spawn_AddsEntity` | FullMemorySndSceneHost.CreateEntity 添加实体并可查找 | ISndSceneHost |
| `SpawnMany_AddsAll` | 创建多个实体后全部存在 | ISndSceneHost |
| `DeadByName_RemovesEntity_FiresBeforeDead` | RemoveEntity 移除实体并触发 BeforeDead | ISndSceneHost |
| `Dispose_RemovesDirectHostEntities_FiresBeforeQuit` | Dispose 触发 BeforeQuit 并清空宿主实体 | ISessionRun |
| `ProcessAll_FiresProcessOnEntities` | ProcessAllSessions 触发 Process 策略 | ISessionManager |
| `SerializeMetaList_ReturnsAllEntities` | BuildMetaList 返回所有实体元数据 | ISndSceneHost |
| `PersistLevelState_WritesPayloadToFileSystem` | Save 后关卡文件存在 | session-model |
| `FullWorkflow_CreatePopulateTickSave` | 完整流程：创建→填充实体→Process→设置黑板→Save→验证文件 | session-model |
| `SerializeToPayload_ReturnsLevelPayload_WithCorrectLevelIdAndData` | Save 后序列化输出含正确 levelId 和实体/黑板数据 | session-model |
| `LoadFromPayload_RestoresSessionState` | 保存后文件存在可验证 | session-model |
| `SerializeToPayload_ThenLoadFromPayload_RoundTrips` | 序列化→反序列化后黑板数据和实体不变 | session-model |
| `LoadFromPayload_Throws_WhenDisposed` | Dispose 后保存不产生关卡文件 | session-model |
| `SerializeToPayload_Throws_WhenDisposed` | Dispose 后保存不产生关卡文件 | session-model |
| `CreateBackgroundSession_ThenLoadSessionFromPayload_RestoresState` | 保存→会话中存在实体和黑板数据 | session-model |
| `FullMemorySndSceneHost_ProcessAll_FiresProcess` | FullMemorySndSceneHost 的 ProcessAll 触发 Process | ISndSceneHost |
| `FullMemorySndSceneHost_LoadFromMetaList_ClearsAndLoads` | RemoveAllEntities + RecoverFromMetaList + FireAfterLoadHooks | ISndSceneHost |
| `BuildSavePayload_IncludesBackgroundSessionsInPayload` | BuildSavePayload 含后台关卡数据 | session-model |
| `SaveAndLoad_RoundTrips_BackgroundSessions` | 前后台保存→Dispose→重新加载完整往返 | session-model |
| `BuildSavePayload_WithNoBackgroundSessions_ClearsBackgroundLevelIds` | 无后台时 Payload 仅含前台 | session-model |
| `BuildSavePayload_IncludesSyncProcessInBackgroundLevelIds` | Payload 中 syncProcess 标志正确 | session-model |
| `SaveAndLoad_RoundTrips_SyncProcessFlag` | 往返后 syncProcess 标志正确恢复 | session-model |
| `SaveAndLoad_FromDisk_RestoresBackgroundSessions` | 写磁盘→读快照→加载→后台恢复 | session-model |
| `ReadFromCurrent_IncludesAllLevelDirectories` | ReadFromCurrent 包含所有关卡目录（含后台） | ISaveStorageService |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `CreateBackgroundSession_Throws_WhenLevelIdInvalid` | null/空/空白 levelId | ArgumentException |
| `LoadSessionFromPayload_Throws_WhenPayloadNull` | 空 levelId | ArgumentException |
| `Dispose_ClearsEntities` | Dispose 后 FindByName | ObjectDisposedException |
| `DisposedSession_ThrowsOnAllPublicMethods` | Dispose 后 SessionBlackboard/StateMachines/FindByName/GetEntities | ObjectDisposedException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `FindByName_ReturnsNullWhenNotFound` | 查找不存在的实体 | 返回 null |
| `Dispose_IsIdempotent` | 两次 Dispose | 不抛异常 |

## BackgroundSession_CreationWithCorrectFlagTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GivenSessionManager_WhenCreateBackgroundSession_ThenIsFrontSessionIsFalse` | 后台 IsFrontSession 为 false | ISessionRun.IsFrontSession |
| `GivenSessionManager_WhenCreateBackgroundWithSync_ThenIsFrontSessionIsFalse` | syncProcess=true 后台 IsFrontSession 仍为 false | ISessionRun.IsFrontSession |

## BackgroundSession_MultipleInstancesAllowedTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GivenSessionManager_WhenCreateMultipleBackgroundSessions_ThenAllCreatedSuccessfully` | 同时创建 3 个后台全部成功 | ISessionManager |
| `GivenSessionManager_WhenMultipleBackgroundSessionsExist_ThenForegroundStillIsFront` | 多后台时不影响前台的 IsFrontSession=true | ISessionRun.IsFrontSession |

## FrontSession_CreationWithCorrectFlagTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GivenSessionManager_WhenCreateForegroundSession_ThenIsFrontSessionIsTrue` | 前台 IsFrontSession 为 true | ISessionRun.IsFrontSession |
| `GivenSessionManager_WhenCreateForegroundFromPayload_ThenIsFrontSessionIsTrue` | 保存后前台 IsFrontSession 仍为 true | ISessionRun.IsFrontSession |
| `GivenSessionManager_WhenSwitchForeground_ThenNewForegroundStillIsFrontSession` | 切换后新前台 IsFrontSession 为 true | ISessionRun.IsFrontSession |

## FrontSession_StrategyContextReceivesFrontFlagTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GivenGlobalSndContext_WhenForegroundMounted_ThenContextIsFrontSessionIsTrue` | 挂载前台后 ForegroundSession.IsFrontSession 为 true | ISessionRun.IsFrontSession |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `GivenGlobalSndContext_WhenNoForeground_ThenContextIsFrontSessionIsFalse` | 无前台时 ForegroundSession 为 null | 返回 null |

## FrontSession_UniqueConstraintValidationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GivenSessionManager_WhenCreateForegroundTwice_ThenOldForegroundReplaced` | 创建新前台替换旧前台 | ISessionManager |
| `GivenSessionManager_WhenForegroundExists_ThenOnlyOneForegroundKey` | Keys 中仅一个 __foreground__ 键 | ISessionManager.ForegroundKey |
| `GivenSessionManager_WhenForegroundAndBackgroundExist_ThenOnlyForegroundHasFlag` | 前后台共存时仅前台 IsFrontSession=true | ISessionRun.IsFrontSession |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `BeforeSaveSpyStrategy` | DisposeSemanticsTests.cs | 重写 BeforeSave 钩子，通过 AsyncLocal<List<string>> 记录调用 |
| `BeforeQuitSpyStrategy` | DisposeSemanticsTests.cs | 重写 BeforeQuit 钩子，通过 AsyncLocal<List<string>> 记录调用 |
| `SessionAccessQuitStrategy` | DisposeSemanticsTests.cs | 在 BeforeQuit 中验证 SceneHost 和 SessionBlackboard 仍可访问 |
| `ThrowingQuitStrategy` | DisposeSemanticsTests.cs | 在 BeforeQuit 中故意抛异常，验证异常安全 |
| `ContractPushStrategy` | ForegroundBackgroundContractTests.cs | StateMachineStrategyBase：OnPushRuntime 记录 BeforeTop→AfterTop 事件 |
| `ContractPopStrategy` | ForegroundBackgroundContractTests.cs | 空白 Pop 策略（仅占位） |
| `TrackingStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase：记录 AfterSpawn/AfterLoad/AfterAdd/BeforeRemove/BeforeSave/BeforeQuit/BeforeDead 全部钩子调用 |
| `ProcessCounterStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase：Process 钩子调用 AsyncLocal<Action> |
| `SessionContextSpyStrategy` | BackgroundSessionTests.cs | LifecycleStrategyBase：Process 钩子记录 OwningSession.LevelId |
| `FindByNameStrategy` | SaveAndSwitchForegroundIntegrationTests.cs | LifecycleStrategyBase：在 AfterSpawn/AfterLoad 钩子中通过 OwningSession.FindByName 查找自身和兄弟实体 |
| `BlackboardProbeStrategy` | SessionDecouplingTests.cs | StateMachineStrategyBase：OnPushRuntime 读取 SessionBlackboard 中的 marker 键 |
| `SceneAccessProbeStrategy` | SessionDecouplingTests.cs | StateMachineStrategyBase：OnPushRuntime 读取 SceneAccess 中的全部实体名称 |
| `NoOpPopStrategy` | SessionDecouplingTests.cs | 空白 Pop 策略（仅占位，配合 BlackboardProbeStrategy/SceneAccessProbeStrategy） |
| `PrefixedSavePathPolicy` | SessionDecouplingTests.cs | ISavePathPolicy 实现：所有路径段加前缀，验证策略注入能力 |
| `TrackingSaveStorageService` | SessionDecouplingTests.cs | ISaveStorageService 装饰器：记录 WriteLevelPayloadOnly 调用次数和最后 Payload |
| `TickProbeStrategy` | PlayStopPlayRoundTripTests.cs | LifecycleStrategyBase：Process 时将 OwningSession.LevelId 记入 AsyncLocal 集合，用于间接验证 syncProcess（哪些会话被 ProcessAllSessions 处理） |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 后台会话与前台共享同一 levelId 时的冲突自动解决 | SwitchForeground 自动保存销毁后台 | session-model: levelId 唯一性约束 |
| ISessionManager.ProcessAllSessions includeForeground=true 的行为 | 前台是否参与 ProcessAll | ISessionManager |
| 大量后台会话（100+）时的性能边界 | 极端并发会话数 | — |
| ProgressRun.LoadFromPayload 对 Payload.Levels 为 null 的处理 | null Levels 的防御 | session-model |
| 会话双层 Dispose 时 ForegroundSession 与外部引用的竞态 | 外部持有 ISessionRun 引用在 Dispose 后使用 | — |
| SessionTopologyCodec 对含逗号的 key/levelId 的解析 | key 或 levelId 中的逗号作为分隔符的特殊值 | SessionTopologyCodec |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
