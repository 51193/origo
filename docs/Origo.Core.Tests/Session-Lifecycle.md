# 会话生命周期 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Runtime/Lifecycle](../Origo.Core/Runtime/Lifecycle/README.md)
> [↔ 被测行为: usage/session-model](../usage/session-model.md)

## 被测行为概览

验证 Origo 会话模型的完整生命周期：SessionRun 创建/销毁/Dispose 语义、
ProgressRun 的 LoadFromPayload/SwitchForeground/PersistProgress、
前后台会话的接口和行为一致性、会话拓扑编解码、前台唯一性约束。

> **批量生命周期：** SessionRun 在创建/销毁/切换时通过整体操作（SpawnEntity/RecoverFromMetaList/RemoveAllEntities）管理实体，
> AfterSpawn/AfterLoad/BeforeDead 钩子由 SessionRun/ProgressRun 生命周期在批量操作完成后集中触发，而非逐实体执行。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `LifecycleRunsTests.cs` | SessionRun/ProgressRun 生命周期、MountKey、PersistLevelState、SwitchForeground |
| `DisposeSemanticsTests.cs` | Dispose 不触发 BeforeSave、触发 BeforeQuit、幂等、Save-then-Dispose-then-Continue 往返 |
| `ForegroundBackgroundContractTests.cs` | 前后台 ISessionRun 行为完全一致（黑板/状态机/序列化/Dispose） |
| `EmptySessionManagerTests.cs` | EmptySessionManager 的无操作行为 |
| `PlayStopPlayRoundTripTests.cs` | 多次 Play→Stop→Play 的往返一致性 |
| `ProgressRunSessionLoadingEdgeTests.cs` | ProgressRun 加载边缘路径 |
| `SaveAndSwitchForegroundTests.cs` | 保存+切换关卡的组合操作 |
| `SessionDecouplingTests.cs` | 会话独立运行互不干扰 |
| `SessionManagerTests.cs` | ISessionManager：创建/查找/销毁/枚举/ProcessAll |
| `SessionTopologyCodecTests.cs` | SessionTopology 编解码往返 |
| `BackgroundSessionTests.cs` | 后台会话独立测试 |
| `BackgroundSession_CreationWithCorrectFlagTests.cs` | 后台 IsFrontSession=false |
| `BackgroundSession_MultipleInstancesAllowedTests.cs` | 后台可多实例 |
| `BackgroundSession_StrategyContextReceivesBackgroundFlagTests.cs` | 策略上下文接收后台标志 |
| `FrontSession_CreationWithCorrectFlagTests.cs` | 前台 IsFrontSession=true |
| `FrontSession_StrategyContextReceivesFrontFlagTests.cs` | 策略上下文接收前台标志 |
| `FrontSession_UniqueConstraintValidationTests.cs` | 前台唯一性约束 |

## LifecycleRunsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SessionRun_Dispose_ClearsSessionAndScene_ThenThrowsOnAccess` | Dispose 后 SessionBlackboard/SceneHost/StateMachines 访问抛 ObjectDisposedException | session-model: Dispose 语义 |
| `ProgressRun_LoadFromPayload_RestoresProgressAndSession` | 从 Payload 恢复 Progress 黑板和 Session 黑板 | persistence-flow |
| `ProgressRun_SwitchForegroundLevel_PersistsOldSession_AndLoadsNewSessionFromCurrent` | 切换关卡时旧会话显式持久化、新会话从 current/ 加载 | session-model: 关卡切换 |
| `ProgressRun_SwitchForegroundLevel_WhenTargetMissing_EntersEmptySessionAndClearsScene` | 目标关卡无数据时进入空会话、清空 Scene | session-model |
| `ProgressRun_LoadAndMountForeground_SyncsSessionTopologyToProgressBlackboard` | LoadAndMountForeground 后拓扑写入 ProgressBlackboard | session-model: 会话拓扑 |
| `SessionRun_SerializeToPayload_RoundTrip_PreservesBlackboardData` | 序列化→反序列化黑板数据不丢失 | persistence-flow |
| `LoadAndMountForeground_WhenNoPayloadFound_MountsEmptySession` | 无存档数据时加载空会话 | session-model |
| `ResolveLevelPayload_ReturnsNull_WhenNoData` | 无数据时返回 null | ISaveStorageService |
| `SessionRun_Create_LogsCreation` | 创建 SessionRun 时记录日志 | Logging |
| `ProgressRun_Create_LogsCreation` | 创建 ProgressRun 时记录日志 | Logging |
| `SessionManager_Mount_LogsMounting` | 挂载会话时记录日志 | Logging |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ProgressRun_LoadFromPayload_WithEmptyProgressNode_ThrowsMissingSessionTopology` | progress.json 无拓扑字段 | InvalidOperationException |
| `ProgressRun_BuildSavePayload_ThrowsWhenProgressTopologyForegroundDoesNotMatchForeground` | 拓扑中的前台与实际前台不匹配 | InvalidOperationException |
| `SessionRun_LoadFromPayload_WhenSceneLoadFails_ResetsSessionState` | Scene 加载失败 | Exception + Session 状态回滚 |
| `ProgressRun_LoadFromPayload_MissingProgressStateMachinesNode_Throws` | Payload 缺 ProgressStateMachinesNode | InvalidOperationException |
| `LoadAndMountForeground_WithEmptyLevelId_Throws` | 空 levelId | ArgumentException |
| `SwitchForeground_WithEmptyLevelId_Throws` | 空 levelId | ArgumentException |
| `BuildSavePayload_WithoutTopologySet_Throws` | ProgressBlackboard 无拓扑 | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SessionRun_MountKey_IsNull_WhenNotMounted` | 创建会话后销毁，通过 ISessionManager.Contains 验证已卸载 | 返回 false |
| `SessionRun_MountKey_SetOnMount_ClearedOnUnmount` | 创建会话后 Contains 为 true，销毁后为 false | ISessionManager 正确管理挂载 |
| `SessionRun_Dispose_AutoUnmountsFromManager` | Dispose 时自动从 Manager 卸载，Contains 返回 false | ISessionManager 自动清理 |
| `SessionManager_Clear_EmptiesAllSessions` | 逐个 DestroySession 后 Keys 为空 | 全部清空 |

## DisposeSemanticsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SessionRun_Dispose_DoesNotWriteFilesToCurrent` | Dispose 不自动写入文件（通过 ISaveStorageService 检查） | session-model: Dispose 不持久化 |
| `SessionRun_Dispose_DoesNotTriggerBeforeSave` | Dispose 不触发 BeforeSave 钩子 | session-model: Dispose 不持久化 |
| `SessionRun_Dispose_TriggersBeforeQuit` | Dispose 触发 BeforeQuit 钩子 | session-model |
| `SessionRun_Dispose_BeforeQuit_CanAccessSceneHost` | BeforeQuit 中可安全访问 ctx.CurrentSession.SceneHost 和 SessionBlackboard（不抛 ObjectDisposedException） | strategy-lifecycle: BeforeQuit 会话访问保证 |
| `SessionRun_ExplicitPersistLevelState_WritesToCurrent_BeforeDispose` | 通过 ISndContext.RequestSaveGame 持久化写入文件 | session-model |
| `SessionRun_ExplicitPersistLevelState_TriggersBeforeSave` | 通过 ISndContext.RequestSaveGame 触发 BeforeSave | session-model |
| `ProgressRun_Dispose_DoesNotCallPersistProgress` | ProgressRun.Dispose 不调用 PersistProgress | session-model |
| `ProgressRun_Dispose_DeletesCurrentDirectory` | ProgressRun.Dispose 删除 current/ | session-model |
| `ExplicitSave_ThenDispose_ThenContinue_LoadsSavedState` | 显式保存→Dispose→Continue 往返 | persistence-flow |
| `Save_ThenDispose_ThenContinue_ProgressBlackboardPreserved` | ProgressBlackboard 数据在 Continue 后保留 | persistence-flow |
| `SaveAfterSwitch_HasCorrectActiveLevel` | 切换后保存的 ActiveLevelId 正确 | session-model |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SessionRun_Dispose_Twice_IsIdempotent` | 两次 Dispose 不抛异常 | 幂等 |
| `ProgressRun_Dispose_Twice_IsIdempotent` | 两次 Dispose 不抛异常 | 幂等 |
| `ProgressRun_Dispose_DeletesCurrentDirectory_EvenWhenEmpty` | 空 current/ 时 Dispose 安全 | 幂等 |
| `ProgressRun_Dispose_SafeEvenWhenNoCurrentDirectory` | 无 current/ 时 Dispose 安全 | 幂等 |
| `SessionRun_Dispose_BeforeQuitThrows_EntitiesStillRemoved` | BeforeQuit 抛异常后实体仍被移除（try/finally 保证） | 异常安全 |
| `SessionRun_Dispose_BeforeQuitThrows_DoubleDisposeStillIdempotent` | BeforeQuit 抛异常后再次 Dispose 不抛异常 | 幂等 + 异常安全 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SessionRun_AfterDispose_SaveDoesNotPersistSessionData` | Dispose 后保存不包含已释放会话的数据 | 文件不存在 |
| `SessionRun_AfterDispose_SaveExcludesDisposedSession` | Dispose 后 RequestSaveGame 排除已释放会话 | 文件不存在 |
| `SessionRun_AfterDispose_SessionBlackboard_ThrowsObjectDisposed` | Dispose 后访问黑板（ISessionRun 公共属性） | ObjectDisposedException |
| `SessionRun_AfterDispose_SceneHost_ThrowsObjectDisposed` | Dispose 后访问 SceneHost（ISessionRun 公共属性） | ObjectDisposedException |
| `SessionRun_AfterDispose_GetSessionStateMachines_ThrowsObjectDisposed` | Dispose 后获取状态机（ISessionRun 公共方法） | ObjectDisposedException |
| `ProgressRun_AfterDispose_ForegroundSession_IsNull` | Dispose 后 ForegroundSession 为 null | — |
| `ProgressRun_AfterDispose_SessionManagerKeys_IsEmpty` | Dispose 后 Keys 为空 | — |
| `ProgressRun_AfterDispose_ProgressBlackboard_IsCleared` | Dispose 后 ProgressBlackboard 清空 | — |

## ForegroundBackgroundContractTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateBackgroundSession_ReturnsISessionRun_NotConcreteType` | 后台会话作为 ISessionRun 暴露 | session-model: 前后台共享接口 |
| `ForegroundSession_ExposedAsISessionRun` | 前台会话作为 ISessionRun 暴露 | session-model |
| `SerializeToPayload_ProducesSameFormat_ForForegroundAndBackground` | 序列化格式一致 | session-model |
| `LoadFromPayload_WorksIdentically_ForForegroundAndBackground` | 反序列化行为一致 | session-model |
| `SessionBlackboard_ReadWrite_IdenticalBehavior` | 黑板读写行为一致 | session-model |
| `SessionBlackboard_Isolated_BetweenForegroundAndBackground` | 前后台黑板数据隔离 | session-model |
| `Dispose_ThrowsOnAccess_ForBothForegroundAndBackground` | 前后台 Dispose 后访问行为一致 | session-model |
| `StateMachines_WorkIdentically_ForForegroundAndBackground` | 前后台状态机触发策略钩子行为一致 | session-model |
| `PersistLevelState_WritesToStorage_ForBothForegroundAndBackground` | 前后台 PersistLevelState 行为一致 | session-model |
| `BusinessCode_CanTreatBothSessionsIdentically_ThroughInterface` | 业务代码可统一通过 ISessionRun 操作前后台 | session-model |
| `RoundTrip_SerializeAndLoad_IdenticalBetweenForegroundAndBackground` | 前后台序列化往返一致 | session-model |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `BeforeSaveSpyStrategy` | DisposeSemanticsTests.cs | 重写 BeforeSave 钩子，记录调用 |
| `BeforeQuitSpyStrategy` | DisposeSemanticsTests.cs | 重写 BeforeQuit 钩子，记录调用 |
| `ContractPushStrategy` | ForegroundBackgroundContractTests.cs | 重写 OnPushRuntime 钩子，记录事件 |
| `ContractPopStrategy` | ForegroundBackgroundContractTests.cs | 空白 Pop 策略 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 后台会话与前台共享同一 levelId 时的冲突自动解决 | SwitchForeground 自动保存销毁后台 | session-model: levelId 唯一性约束 |
| ISessionManager.ProcessAllSessions includeForeground=true 的行为 | 前台是否参与 ProcessAll | ISessionManager |
| 大量后台会话（100+）时的性能边界 | 极端并发会话数 | — |

---

[↑ 回到 Origo.Core.Tests](README.md)
