# 持久化：存储 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Save/Storage](../Origo.Core/Save/Storage/README.md)
> [↔ 被测行为: usage/persistence-flow](../usage/persistence-flow.md)

## 被测行为概览

验证 Origo 持久化系统的存储层契约："严格读取、显式失败、两阶段写入"。
覆盖 `.write_in_progress` marker、关卡三件套完整性、`progress.json` 缺失、
快照创建/读取往返、路径策略自定义、幂等去重。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `SaveStorageContractTests.cs` | 存储契约：marker、三件套、两阶段写入、快照、meta.map、路径策略 |
| `SaveStorageAndPayloadTests.cs` | 存储与 Payload 集成：写入/读取往返、LevelPayload 写/读 |
| `SaveIdempotencyTests.cs` | 幂等去重：相同 payload 覆盖写入不重复创建 |
| `SavePathLayoutTests.cs` | 路径布局：默认策略下的目录文件路径拼装 |
| `SavePathPolicyContractTests.cs` | 路径策略接口契约：各方法返回有效相对路径 |

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
| `EnumerateSaveIds_ReturnsCorrectList` | 枚举存档不包含 current 目录 | ISaveStorageService |
| `DeleteCurrentDirectory_RemovesAllCurrentFiles` | DeleteCurrentDirectory 删除所有 current/ 内容 | ISaveStorageService |
| `DeleteCurrentDirectory_WhenNoDirectory_DoesNotThrow` | 无目录时 DeleteCurrentDirectory 幂等 | ISaveStorageService |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ReadSavePayloadFromCurrent_WhenWriteInProgressMarkerExists_Throws` | current/ 下有 .write_in_progress 文件 | InvalidOperationException |
| `TryReadLevelPayload_OnlySndSceneExists_Throws` | 关卡只有 snd_scene.json | InvalidOperationException（数据损坏） |
| `TryReadLevelPayload_OnlySessionExists_Throws` | 关卡只有 session.json | InvalidOperationException（数据损坏） |
| `TryReadLevelPayload_OnlyStateMachinesExists_Throws` | 关卡只有 state_machines.json | InvalidOperationException（数据损坏） |
| `TryReadLevelPayload_AnyTwoOfThree_Throws` | 关卡三件套只存在任意两件 | InvalidOperationException（数据损坏） |
| `ReadSavePayloadFromCurrent_WhenProgressJsonMissing_Throws` | progress.json 缺失 | Exception |
| `ReadSavePayloadFromSnapshot_WhenSaveNotExist_Throws` | 不存在的存档快照 | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TryReadLevelPayload_AllThreeMissing_ReturnsNull` | 关卡三件套全缺 | 返回 null（视为无存档） |
| `TryReadLevelPayload_AllThreePresent_Succeeds` | 关卡三件套全存 | 返回完整 LevelPayload |
| `StaleWriteMarker_AfterDeleteCurrentDirectory_WriteThenSucceeds` | stale marker → DeleteCurrentDirectory → 重写 | 新数据可正常写入和读取 |
| `RecoverFromStaleWriteMarker_CleanStateAfterRecovery` | 恢复后 current/ 状态干净 | 无 marker 残留，数据正常 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 阶段2中 rename 失败的原子性 | 快照过程的 .tmp 残留清理 | persistence-flow: 阶段2描述 |
| SaveStorageFacade 写入时并发请求的排队行为 | 并发安全 | — |

## 设计决策

### 为什么使用 TestFileSystem 而非真实文件系统

文档明确：Core 层所有文件操作通过 `IFileSystem` 进行，禁止直接 `File.*` API。
因此测试不应依赖真实文件系统——这会破坏 Core 层的平台无关性。

### 为什么不测试 DefaultSaveStorageService 的内部实现细节

`DefaultSaveStorageService` 是 internal 类型。测试通过注入到 SndContext 的
`ISaveStorageService` 接口验证行为，而非直接测试内部实现。

### 为什么需要单独的 SaveStorageContractTests

原来的持久化测试分散在 `LifecycleRunsTests`、`DisposeSemanticsTests`、
`SndContextWorkflowTests` 中。`SaveStorageContractTests` 将文档中描述的 7 条
严格读取规则集中测试，使契约验证成为独立的、可审计的测试单元。

---

[↑ 回到 Origo.Core.Tests](README.md)
