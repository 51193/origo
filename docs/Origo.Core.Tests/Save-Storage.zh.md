<!-- docsync-pair: Origo.Core.Tests/Save-Storage -->
<!-- docsync-revision: 9 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 持久化：存储 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Save/Storage](../Origo.Core/Save/Storage/README.zh.md)
> [↔ 被测行为: usage/persistence-flow](../usage/persistence-flow.zh.md)

## 被测行为概览

验证 Origo 持久化系统的存储层契约："严格读取、显式失败、两阶段写入"。
覆盖 `.write_in_progress` marker、关卡三件套完整性、`progress.json` 缺失、
快照创建/读取往返、路径策略自定义、幂等去重、Payload 模型默认值、
WellKnownKeys 常量、SaveFileHandle 路径解析与遍历保护。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `SaveStorageContractTests.cs` | 存储契约：marker、三件套、两阶段写入、快照、meta.map、路径策略 |
| `SaveStorageAndPayloadTests.cs` | 存储与 Payload 集成：写入/读取往返、LevelPayload 写/读、快照原子性、错误恢复 |
| `SaveIdempotencyTests.cs` | 幂等去重：Payload SHA 计算、相同/不同 payload 的写入/跳过、SHA 文件管理 |
| `SavePathLayoutTests.cs` | 路径布局：默认策略下的目录文件路径拼装与无效参数守卫 |
| `SavePathPolicyContractTests.cs` | 路径策略接口契约：ISavePathPolicy 注入与全部存储方法的策略感知验证 |
| `SavePathResolverTests.cs` | 路径解析：SaveFileHandle 相对路径提取、父目录创建、遍历攻击拒绝、叶目录名 |
| `SaveGamePayloadTests.cs` | 数据模型：SaveGamePayload/LevelPayload 默认值、多关卡访问、CustomMeta |
| `SaveExtraFilesRoundTripTests.cs` | extra/ 侧信道文件：快照→current 复制往返、目录结构保留、缺失/空目录容错、参数校验 |
| `SaveFormatVersionTests.cs` | 存档格式版本：meta.map 写入 origo.format_version、新版本拒绝加载、缺版本键兼容、保留键隐藏 |
| `SaveSnapshotMarkerTests.cs` | 快照完整性：快照目录无 .write_in_progress 残留 |
| `StaleLevelDirectoryCleanupTests.cs` | 回归：完整保存后 `current/` 与 payload 关卡集合一致——销毁后台会话后其关卡目录被清理，不泄漏进后续快照 |
| `WellKnownKeysTests.cs` | 常量：ActiveSaveId、SessionTopology 键名正确性 |
| `SaveIdValidationTests.cs` | save id 校验：`RequestSaveGame`/`RequestLoadGame`/`SetContinueTarget` 拒绝非法 id（含路径分隔符/越界字符），合法 id 接受 |

## SaveStorageContractTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `WriteSaveToCurrent_CreatesMarkerDuringWrite` | WriteToCurrent 写入后 progress.json 存在、marker 已删除 | persistence-flow: 阶段1 |
| `ReadSavePayloadFromCurrent_WhenNoMarker_Succeeds` | 无 marker 时正常读取 Payload | persistence-flow: 严格读取 |
| `WriteSavePayloadToCurrent_WritesAllExpectedFiles` | 写入后 current/ 下包含全部预期文件 | persistence-flow: 文件布局 |
| `WriteSavePayloadToCurrentThenSnapshot_CreatesSnapshotDirectory` | 快照阶段创建 save_x/ 目录及内容 | persistence-flow: 阶段2 |
| `WriteSavePayloadToCurrentThenSnapshot_ThenReadBackRoundTrip` | 快照 → 读取往返数据一致 | persistence-flow |
| `SnapshotCurrentToSave_WritesAllLevelFiles` | SnapshotCurrentToSave 写入全部关卡文件 | persistence-flow: 阶段2 |
| `WriteSavePayloadToCurrent_ValidPayload_WritesSuccessfully` | 有效 Payload 正确写入 current/ | SaveGamePayload 模型 |
| `WriteSavePayloadToCurrent_EmptySaveId_StillWrites` | 空 SaveId 时仍写入 | SaveGamePayload 模型 |
| `WriteSavePayloadToCurrentThenSnapshot_WithCustomMeta_WritesMetaMap` | CustomMeta 非空时写入 meta.map | persistence-flow: meta.map |
| `WriteSavePayloadToCurrentThenSnapshot_WithoutCustomMeta_MetaMapNotCreated` | CustomMeta 为 null 时不创建 meta.map | persistence-flow: meta.map |
| `DefaultSaveStorageService_WithCustomPathPolicy_UsesCustomLayout` | ISavePathPolicy 自定义路径布局生效 | ISavePathPolicy |
| `EnumerateSaveIds_ReturnsCorrectList` | 枚举存档不包含 current 目录 | ISaveStorageService |
| `DeleteCurrentDirectory_RemovesAllCurrentFiles` | DeleteCurrentDirectory 删除所有 current/ 内容 | ISaveStorageService |
| `TryReadLevelPayload_AllThreePresent_Succeeds` | 关卡三件套全存时返回完整 LevelPayload | persistence-flow: 严格读取 |
| `WriteProgressOnlyToCurrent_RemovesMarkerOnSuccess` | progress 单写成功后无 marker 残留、文件存在 | persistence-flow: 两阶段写入 |
| `WriteLevelPayloadOnlyToCurrent_RemovesMarkerOnSuccess` | 关卡单写成功后无 marker、可读回 | persistence-flow: 两阶段写入 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ReadSavePayloadFromCurrent_WhenWriteInProgressMarkerExists_Throws` | current/ 下有 .write_in_progress 文件 | InvalidOperationException |
| `TryReadLevelPayload_OnlySndSceneExists_Throws` | 关卡只有 snd_scene.json | InvalidOperationException（数据损坏） |
| `TryReadLevelPayload_OnlySessionExists_Throws` | 关卡只有 session.json | InvalidOperationException（数据损坏） |
| `TryReadLevelPayload_OnlyStateMachinesExists_Throws` | 关卡只有 session_state_machines.json | InvalidOperationException（数据损坏） |
| `TryReadLevelPayload_AnyTwoOfThree_Throws` | 关卡三件套只存在任意两件 | InvalidOperationException（数据损坏） |
| `ReadSavePayloadFromCurrent_WhenProgressJsonMissing_Throws` | progress.json 缺失 | 抛出异常 |
| `ReadSavePayloadFromSnapshot_WhenSaveNotExist_Throws` | 不存在的存档快照 | InvalidOperationException |
| `WriteProgressOnlyToCurrent_Failure_LeavesMarkerSoReadersReject` | progress 单写中途 I/O 失败（模拟第二次写入抛异常） | IOException 传播，marker 残留使读端拒绝 |
| `WriteLevelPayloadOnlyToCurrent_Failure_LeavesMarkerSoReadersReject` | 关卡单写 Payload 校验失败（节点为 Null） | InvalidOperationException，marker 残留使读端拒绝 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TryReadLevelPayload_AllThreeMissing_ReturnsNull` | 关卡三件套全缺 | 返回 null（视为无存档） |
| `StaleWriteMarker_AfterDeleteCurrentDirectory_WriteThenSucceeds` | stale marker → DeleteCurrentDirectory → 重写 | 新数据可正常写入和读取 |
| `RecoverFromStaleWriteMarker_CleanStateAfterRecovery` | 恢复后 current/ 状态干净 | 无 marker 残留，数据正常 |
| `DeleteCurrentDirectory_WhenNoDirectory_DoesNotThrow` | current/ 不存在时调用 Delete | 不抛异常（幂等） |

## SaveFormatVersionTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Save_WritesFormatVersionToMetaMap` | 保存时 meta.map 写入 `origo.format_version: 1` | persistence-flow: meta.map |
| `ListSaves_HidesFrameworkReservedMetaKeys` | ListSaves/EnumerateSavesWithMetaData 隐藏 `origo.*` 框架保留键 | persistence-flow: meta.map |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Load_RejectsSaveWithNewerFormatVersion` | 存档格式版本高于当前（99） | InvalidOperationException（加载被拒绝） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Load_AcceptsMissingFormatVersionKey` | 旧存档 meta.map 无版本键 | 视为版本 1 正常加载 |

## SaveSnapshotMarkerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Snapshot_DoesNotContainWriteInProgressMarker` | 完整保存后快照目录内无 `.write_in_progress` 文件 | persistence-flow: 两阶段写入 |

## StaleLevelDirectoryCleanupTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SaveAfterDestroyingBackgroundSession_PrunesStaleLevelDirectory` | 前台 + 后台会话各一关卡保存后，销毁后台再保存：`current/` 与新快照均不含已销毁会话的关卡目录，存活关卡保留 | Save/Storage: 清理 stale 关卡目录 |

## SaveStorageAndPayloadTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SaveStorageFacade_WriteAndReadCurrent_RoundTrip` | WriteToCurrent → ReadFromCurrent 往返数据一致 | ISaveStorageService |
| `SaveStorageFacade_ReadProgressNodeFromSnapshot_WhenPresent_ReturnsContent` | 快照存在时读回 progress 节点内容 | ISaveStorageService |
| `SaveStorageFacade_EnumerateSavesWithMetaData_SlotWithoutMetaMap_StillListed` | 无 meta.map 的存档槽位仍然被列出 | ISaveStorageService |
| `SaveStorageFacade_SnapshotCurrentToSave_AndEnumerateSaveIds_Works` | WriteToCurrent → Snapshot → Enumerate 全流程 | ISaveStorageService |
| `SaveStorageFacade_SnapshotCurrentToSave_UsesTempDirectoryThenRename` | 快照使用 .tmp 目录再 rename，无残留 | persistence-flow: 阶段2 |
| `SnapshotCurrentToSave_OverwritingExistingSave_ReplacesContentAndLeavesNoBackup` | 覆盖已有存档替换内容，无 .bak/.tmp 残留 | ISaveStorageService |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SaveStorageFacade_EnumerateSaveIds_NullFileSystem_Throws` | null IFileSystem | ArgumentNullException |
| `SaveStorageFacade_SnapshotCurrentToSave_WhitespaceSaveRoot_Throws` | 空白存储根路径 | ArgumentException |
| `SaveStorageFacade_SnapshotCurrentToSave_WhitespaceNewSaveId_Throws` | 空白存档 ID | ArgumentException |
| `SaveStorageFacade_ReadSavePayloadFromSnapshot_WhitespaceSaveRoot_Throws` | 空白存储根路径 | ArgumentException |
| `SaveStorageFacade_ReadCurrent_MissingProgressStateMachines_Throws` | progress_state_machines.json 缺失 | InvalidOperationException |
| `SaveStorageFacade_ReadCurrent_MissingSessionStateMachines_Throws` | session_state_machines.json 缺失 | InvalidOperationException |
| `WriteToCurrent_WhenActiveLevelMissing_ThrowsWithoutWritingCurrent` | ActiveLevelId 对应的 LevelPayload 缺失 | InvalidOperationException，current/ 无残留 |
| `SaveStorageFacade_ReadCurrent_ActiveLevelPartial_MissingSession_Throws` | 活跃关卡缺 session.json | InvalidOperationException |
| `SaveStorageFacade_ReadCurrent_BackgroundLevelPartial_MissingStateMachines_Throws` | 后台关卡缺 session_state_machines.json | InvalidOperationException |
| `SaveStorageFacade_ReadCurrent_WhenWriteMarkerExists_Throws` | current/ 下有 .write_in_progress | InvalidOperationException |
| `SavePayloadReader_TryReadLevelPayloadFromCurrent_WhenWriteMarkerExists_Throws` | 读关卡时 .write_in_progress 存在 | InvalidOperationException |
| `DefaultSaveStorageService_ResolveLevelPayload_WhenWriteMarkerExists_Throws` | ResolveLevelPayload 时 .write_in_progress 存在 | InvalidOperationException |
| `WriteSavePayloadToCurrentThenSnapshot_NullLogger_Throws` | null logger | ArgumentNullException |
| `WriteSavePayloadToCurrentThenSnapshot_WhenSnapshotFails_LogsError_LeavesMarkerAndUpdatedCurrent` | 快照阶段 Copy 失败 | InvalidOperationException，current/ 保持已写入状态，marker 残留 |
| `SaveStorageFacade_SnapshotCurrentToSave_CleansUpTempOnFailure` | 快照 Copy 失败 | .tmp 目录被清理 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SaveStorageFacade_ReadProgressNodeFromSnapshot_Missing_ReturnsNull` | 快照目录不存在 | 返回 null |
| `SavePayloadReader_TryReadLevelPayloadFromCurrent_AllFilesAbsent_ReturnsNull` | 关卡文件全缺 | 返回 null |

## SaveIdempotencyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ComputePayloadHash_SamePayload_SameHash` | 相同 Payload 产生相同 64 位十六进制 SHA | SavePayloadWriter |
| `ComputePayloadHash_DifferentProgressNode_DifferentHash` | ProgressNode 变化导致 Hash 变化 | SavePayloadWriter |
| `ComputePayloadHash_DifferentLevelContent_DifferentHash` | Level 内 SessionNode 变化导致 Hash 变化 | SavePayloadWriter |
| `ComputePayloadHash_DifferentCustomMeta_DifferentHash` | CustomMeta 值变化导致 Hash 变化 | SavePayloadWriter |
| `ComputePayloadHash_CustomMetaOrder_Independent` | CustomMeta key 顺序不影响 Hash | SavePayloadWriter |
| `ComputePayloadHash_LevelOrder_Independent` | Levels 字典 key 顺序不影响 Hash | SavePayloadWriter |
| `WriteToCurrent_CreatesPayloadShaFile` | WriteToCurrent 写入 .payload.sha 文件 | SavePayloadWriter |
| `WriteSavePayloadToCurrentThenSnapshot_SamePayloadTwice_SecondSkips` | 相同 Payload 二次写入跳过，current/ 不重建 | persistence-flow: 幂等 |
| `WriteSavePayloadToCurrentThenSnapshot_DifferentPayload_Overwrites` | 不同 Payload 正常覆写 | persistence-flow: 幂等 |
| `WriteSavePayloadToCurrentThenSnapshot_NewSaveId_AlwaysWrites` | 新 SaveId 始终写入（无已有 SHA 可比） | persistence-flow |
| `WriteSavePayloadToCurrentThenSnapshot_ExistingSaveNoSha_WritesAndCreatesSha` | 已有快照但无 .payload.sha 时正常写入并创建 SHA | persistence-flow |
| `WriteSavePayloadToCurrentThenSnapshot_CorruptedShaFile_WritesAndOverwrites` | 已有快照 .payload.sha 损坏时覆写正确 SHA | persistence-flow |
| `WriteSavePayloadToCurrentThenSnapshot_WhenWriteMarkerExists_StillThrows` | Hash 不匹配时即使 marker 存在也正常完成 | persistence-flow: 幂等 |
| `SnapshotCurrentToSave_CopiesPayloadShaFile` | SnapshotCurrentToSave 复制 .payload.sha 到快照 | SaveStorageFacade |
| `WriteSavePayloadToCurrentThenSnapshot_IdempotentSkip_PreservesExistingSnapshot` | 幂等跳过时快照内容不变 | persistence-flow: 幂等 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `WriteSavePayloadToCurrentThenSnapshot_ShaReadError_PropagatesException` | SHA 文件读取失败 | InvalidOperationException（传播原始异常） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `ComputePayloadHash_EmptyPayload_Works` | 全部使用空节点的 Payload | 返回合法 64 位十六进制 SHA |
| `ComputePayloadHash_NullCustomMeta_DoesNotThrow` | CustomMeta 为 null | 不抛异常，正常计算 Hash |

## SavePathLayoutTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SavePathLayout_GetCurrentDirectory_ReturnsCurrent` | 返回 "current" | SavePathLayout |
| `SavePathLayout_CurrentDirectoryName_Constant` | 常量值为 "current" | SavePathLayout |
| `SavePathLayout_GetSaveDirectory_FormatsCorrectly` | saveId → "save_{id}" 格式 | SavePathLayout |
| `SavePathLayout_GetProgressFile_CombinesCorrectly` | base → "base/progress.json" | SavePathLayout |
| `SavePathLayout_GetProgressStateMachinesFile_CombinesCorrectly` | base → "base/progress_state_machines.json" | SavePathLayout |
| `SavePathLayout_GetCustomMetaFile_CombinesCorrectly` | base → "base/meta.map" | SavePathLayout |
| `SavePathLayout_GetLevelDirectory_CombinesCorrectly` | base + levelId → "base/level_{levelId}" | SavePathLayout |
| `SavePathLayout_GetLevelSndSceneFile_CombinesCorrectly` | levelDir → "level_dir/snd_scene.json" | SavePathLayout |
| `SavePathLayout_GetLevelSessionFile_CombinesCorrectly` | levelDir → "level_dir/session.json" | SavePathLayout |
| `SavePathLayout_GetLevelSessionStateMachinesFile_CombinesCorrectly` | levelDir → "level_dir/session_state_machines.json" | SavePathLayout |
| `SavePathLayout_GetWriteInProgressMarker_CombinesCorrectly` | base → "base/.write_in_progress" | SavePathLayout |
| `SavePathLayout_WriteInProgressMarkerName_Constant` | 常量值为 ".write_in_progress" | SavePathLayout |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SavePathLayout_GetSaveDirectory_ThrowsOnInvalidId` | null / 空 / 空白 saveId | ArgumentException |
| `SavePathLayout_GetProgressFile_ThrowsOnEmpty` | 空 base 目录 | ArgumentException |
| `SavePathLayout_GetProgressStateMachinesFile_ThrowsOnWhitespace` | 空白 base 目录 | ArgumentException |
| `SavePathLayout_GetCustomMetaFile_ThrowsOnNull` | null base 目录 | ArgumentException |
| `SavePathLayout_GetLevelDirectory_ThrowsOnInvalidArgs` | 空/空白 base 或 levelId | ArgumentException |
| `SavePathLayout_GetLevelSndSceneFile_ThrowsOnEmpty` | 空 level 目录 | ArgumentException |
| `SavePathLayout_GetLevelSessionFile_ThrowsOnWhitespace` | 空白 level 目录 | ArgumentException |
| `SavePathLayout_GetLevelSessionStateMachinesFile_ThrowsOnNull` | null level 目录 | ArgumentException |
| `SavePathLayout_GetWriteInProgressMarker_ThrowsOnEmpty` | 空 base 目录 | ArgumentException |

## SavePathPolicyContractTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndContext_DefaultStorage_Uses_Injected_SavePathPolicy` | SndContext 默认存储使用注入的 ISavePathPolicy | ISavePathPolicy |
| `SndContext_DefaultInitialStorage_Uses_Injected_SavePathPolicy` | SndContext 初始存储使用注入的 ISavePathPolicy | ISavePathPolicy |
| `SystemRuntime_DefaultStorage_Uses_Injected_SavePathPolicy` | SystemRuntime 默认存储使用注入的 ISavePathPolicy | ISavePathPolicy |
| `DefaultSaveStorageService_EnumerateSaveIds_Uses_PathPolicy` | EnumerateSaveIds 通过策略拼装路径 | ISavePathPolicy |
| `DefaultSaveStorageService_EnumerateSavesWithMetaData_Uses_PathPolicy` | EnumerateSavesWithMetaData 通过策略读 meta.map | ISavePathPolicy |
| `DefaultSaveStorageService_WriteSavePayloadToCurrentThenSnapshot_Uses_PathPolicy` | 两阶段写入全部经过策略拼装路径 | ISavePathPolicy |
| `DefaultSaveStorageService_SnapshotCurrentToSave_Uses_PathPolicy` | SnapshotCurrentToSave 经策略拼装快照路径 | ISavePathPolicy |
| `DefaultSaveStorageService_WriteSavePayloadToCurrent_Uses_PathPolicy` | WriteToCurrent 经策略拼装文件路径 | ISavePathPolicy |
| `DefaultSaveStorageService_ReadWriteRoundTrip_Uses_PathPolicy` | 策略定制路径下的写入→读取往返 | ISavePathPolicy |
| `SessionStateMachineContext_SceneAccess_PointsToForegroundSession_ForegroundAndBackground` | 前后台 Session 状态机中 SceneAccess 各自指向自身 SceneHost | StateMachineStrategyBase |
| `SessionStateMachineContext_SessionBlackboard_PointsToForegroundSession_ForegroundAndBackground` | 前后台 Session 状态机中 SessionBlackboard 各自隔离 | StateMachineStrategyBase |

## SaveFileHandleTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SavePathResolver_EnsureParentDirectory_CreatesParent` | 父目录不存在时自动创建 | SaveFileHandle |
| `SavePathResolver_GetRelativePath_ExtractsRelative` | 从绝对路径提取相对部分 | SaveFileHandle |
| `SavePathResolver_GetRelativePath_NestedPath` | 多层嵌套路径提取相对部分 | SaveFileHandle |
| `SavePathResolver_GetLeafDirectoryName_ReturnsLastSegment` | 多段路径返回最后一段 | SaveFileHandle |
| `SavePathResolver_GetLeafDirectoryName_SingleSegment` | 单段路径返回自身 | SaveFileHandle |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SavePathResolver_GetRelativePath_RejectsTraversalInRelativeSegment` | 路径含 `../` 遍历 | ArgumentException |
| `SavePathResolver_GetRelativePath_WhitespaceRoot_ThrowsOnConstruction` | 空/空白存储根路径构造 | ArgumentException |
| `SavePathResolver_RejectPathTraversal_ThrowsOnDotDot` | 输入含 `..` 的多种变体 | ArgumentException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SavePathResolver_EnsureParentDirectory_NoOpForRootFile` | 根目录下的文件无需创建父目录 | 不抛异常 |
| `SavePathResolver_GetRelativePath_ExactMatch_ReturnsEmpty` | 路径恰好等于存储根 | 返回空字符串 |
| `SavePathResolver_GetRelativePath_NoMatch_ReturnsFullPath` | 路径不在存储根下 | 返回完整绝对路径 |
| `SavePathResolver_GetLeafDirectoryName_TrailingSlash` | 尾部带斜杠的路径 | 返回末尾段名 |
| `SavePathResolver_GetLeafDirectoryName_EmptyOrWhitespace_ReturnsEmpty` | 空/空白路径 | 返回空字符串 |
| `SavePathResolver_RejectPathTraversal_AllowsSafePaths` | 安全路径（无 `..`） | 不抛异常 |

## SaveGamePayloadTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CurrentFormatVersion_IsOne` | FormatVersion 常量值为 1 | SaveGamePayload |
| `WithSingleLevel_CanAccessLevel` | 单关卡 Payload 可通过 Levels 字典正确访问 | SaveGamePayload |
| `WithMultipleLevels_AllAccessible` | 多关卡 Payload 全部可访问 | SaveGamePayload |
| `CustomMeta_CanBeSet` | CustomMeta 字典可设置并读取 | SaveGamePayload |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `DefaultValues` | SaveGamePayload 默认构造 | SaveId/ActiveLevelId 为空串，ProgressNode IsNull，Levels 非 null |
| `LevelPayload_DefaultValues` | LevelPayload 默认构造 | LevelId 为空串，所有 Node IsNull |
| `CustomMeta_Null_Allowed` | CustomMeta 设为 null | 允许为 null |

## WellKnownKeysTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `WellKnownKeys_ActiveSaveId_HasExpectedValue` | ActiveSaveId = "origo.active_save_id" | WellKnownKeys |
| `WellKnownKeys_SessionTopology_HasExpectedValue` | SessionTopology = "origo.session_topology" | WellKnownKeys |

## SaveExtraFilesRoundTripTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CopyDirectoryFromSnapshot_SeededFiles_AllCopiedToCurrent` | 快照 save_001/extra 下种子文件全部复制到 current/extra | SaveStorageFacade.CopyDirectoryFromSnapshot |
| `CopyDirectoryFromSnapshot_SubdirectoryStructurePreserved` | 子目录层级结构在复制后原样保留 | SaveStorageFacade.CopyDirectoryFromSnapshot |
| `CopyDirectoryFromSnapshot_ExistingFilesInCurrent_Overwrites` | current 中已有同名文件被快照内容覆盖 | SaveStorageFacade.CopyDirectoryFromSnapshot |
| `ExtraFiles_FullSaveLoadRoundTrip_PreservesMultipleFiles` | 多个 extra 文件保存→加载往返后内容与结构保留 | persistence-flow: extra |
| `ExtraFiles_SaveLoadRoundTrip_SubdirectoryPreserved` | 子目录下的 extra 文件往返保留 | persistence-flow: extra |
| `ExtraFiles_SaveTwice_SameSlot_HasLatestContent` | 同槽位保存两次后加载为最新内容 | persistence-flow: extra |
| `ExtraFiles_SaveLoadRoundTrip_TypeDataRoundTrip_PreservesNumbers` | TypedData 对象写读（int/bool/string）往返保留值 | persistence-flow: extra |
| `ExtraFiles_DeleteFileThenSave_FileNotInSnapshot` | 删除文件后保存，快照不含该文件 | persistence-flow: extra |
| `ExtraFiles_DifferentContent_DifferentCombinedHash` | extra 内容变化导致 .payload.sha 哈希变化 | persistence-flow: 幂等 |
| `ExtraFiles_LoadWithoutExtra_DoesNotThrowAndPreviousStateCleared` | 加载不含 extra 的存档不抛异常 | persistence-flow: extra |
| `IdempotentSkip_UnchangedPayloadAndExtra_SkipHappens` | Payload 与 extra 哈希均未变时二次保存幂等跳过并记录日志 | persistence-flow: 幂等 |
| `CombineHashes_EmptySide_ProducesConsistentFormat` | 空侧哈希参与合并仍产生 64 位十六进制结果 | SaveAtomicWriter |
| `CombineHashes_SamePayload_EmptyAndNonEmptySide_DifferentResult` | 同一 payload 哈希，空侧与非空侧合并结果不同 | SaveAtomicWriter |
| `CombineHashes_WithExtra_DifferentFromPayloadHash` | 合并 extra 哈希后不同于纯 payload 哈希 | SaveAtomicWriter |
| `ComputeSideDirectoryHash_WithFiles_ReturnsNonEmpty` | 目录含文件时返回非空 64 位十六进制哈希 | SaveAtomicWriter |
| `ComputeSideDirectoryHash_SameContent_SameHash` | 相同内容两次计算哈希一致 | SaveAtomicWriter |
| `ComputeSideDirectoryHash_DifferentContent_DifferentHash` | 内容变化后哈希不同 | SaveAtomicWriter |
| `ComputeSideDirectoryHash_CustomDirectoryName_Works` | 自定义目录名同样计算 64 位哈希 | SaveAtomicWriter |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `CopyDirectoryFromSnapshot_SourceDirectoryDoesNotExist_ReturnsSilently` | 快照中无 extra/ 目录 | 静默返回，不抛异常 |
| `CopyDirectoryFromSnapshot_EmptySourceDirectory_DoesNothing` | extra/ 为空目录 | current/extra 创建但内容为空 |
| `ComputeSideDirectoryHash_NoExtraDir_ReturnsEmpty` | 无 extra/ 目录 | 返回空字符串 |
| `ComputeSideDirectoryHash_EmptyExtraDir_ReturnsEmpty` | extra/ 目录存在但为空 | 返回空字符串 |
| `ComputeSideDirectoryHash_CustomDirectory_Empty_ReturnsEmpty` | 自定义目录为空 | 返回空字符串 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `CopyDirectoryFromSnapshot_NullHandle_Throws` | handle 为 null | ArgumentNullException |
| `CopyDirectoryFromSnapshot_EmptySaveId_Throws` | saveId 为空字符串 | ArgumentException |
| `CopyDirectoryFromSnapshot_EmptyDirName_Throws` | relativeDirName 为空字符串 | ArgumentException |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `CustomSavePathPolicy` | SaveStorageContractTests.cs | 自定义 ISavePathPolicy，所有方法生成带前缀的测试路径 |
| `FailOnCopyFileSystem` | SaveStorageAndPayloadTests.cs | 在 Copy 目标路径匹配子串时抛出异常，模拟快照复制失败 |
| `ThrowingOnReadFileSystem` | SaveIdempotencyTests.cs | 在 ReadAllText 访问指定路径时抛异常，模拟 SHA 文件读取失败 |
| `TestPrefixedPathPolicy` | SavePathPolicyContractTests.cs | 带前缀的自定义 ISavePathPolicy，验证策略注入贯穿所有存储方法 |
| `SceneContractStrategy` | SavePathPolicyContractTests.cs | 状态机策略，在 OnPushRuntime 中收集 SceneHost 实体名 |
| `BbContractStrategy` | SavePathPolicyContractTests.cs | 状态机策略，在 OnPushRuntime 中收集 SessionBlackboard 值 |
| `NoOpPopContractStrategy` | SavePathPolicyContractTests.cs | 空实现状态机策略，供契约测试的状态机 Push 使用 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 阶段2中 rename 失败的原子性 | 快照过程的 .tmp 残留清理 | persistence-flow: 阶段2描述 |
| SaveStorageFacade 写入时并发请求的排队行为 | 并发安全 | — |
| 大量关卡文件快照的性能（深目录树递归复制） | 极端场景下的 I/O 表现 | — |

## 设计决策

### 为什么使用 TestMemoryFileSystem 而非真实文件系统

文档明确：Core 层所有文件操作通过 `IFileSystem` 进行，禁止直接 `File.*` API。
因此测试不应依赖真实文件系统——这会破坏 Core 层的平台无关性。

### 为什么不测试 DefaultSaveStorageService 的内部实现细节

`DefaultSaveStorageService` 是 internal 类型。测试通过注入到 SndContext 的
`ISaveStorageService` 接口验证行为，而非直接测试内部实现。

### 为什么需要单独的 SaveStorageContractTests

`SaveStorageContractTests` 将文档中描述的 7 条严格读取规则集中测试
（其余持久化行为测试位于 `LifecycleRunsTests`、`DisposeSemanticsTests`、
`SndContextWorkflowTests` 等文件中），使契约验证成为独立的、可审计的测试单元。

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
