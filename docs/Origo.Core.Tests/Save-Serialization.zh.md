<!-- docsync-pair: Origo.Core.Tests/Save-Serialization -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 持久化：序列化 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Save/Serialization](../Origo.Core/Save/Serialization/README.zh.md)
> [↔ 被测行为: usage/persistence-flow](../usage/persistence-flow.zh.md)

## 被测行为概览

验证存档 Payload 的序列化编排：Blackboard 序列化/反序列化往返、SND 场景实体列表序列化、
SaveContext 在 ProgressRun 流程中的协调行为、SaveCoordinator 构造守卫、
PersistentBlackboard 磁盘读写。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `BlackboardSerializerTests.cs` | Blackboard 序列化/反序列化：TypedData 类型保留、空黑板、覆写语义 |
| `SndSceneSerializerTests.cs` | SND 场景序列化：实体元数据列表 ↔ DataSourceNode 往返、空场景、非法 JSON |
| `SaveContextTests.cs` | SaveContext：Payload 构建/写入编排、Sequence 黑板反序列化原子更新、null 守卫 |
| `SaveCoordinatorTests.cs` | SaveCoordinator：构造函数 null 参数守卫、无前台 Session 时 PersistProgress 拒绝 |
| `PersistentBlackboardTests.cs` | PersistentBlackboard：Set/Clear/LoadFromDisk 磁盘往返 |

## BlackboardSerializerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `BlackboardSerializer_RoundTrip_PreservesData` | int/string 多键值序列化→反序列化，类型和值完全保留 | BlackboardSerializer |
| `BlackboardSerializer_DeserializeInto_OverwritesExisting` | DeserializeInto 用源数据完全替换目标黑板全部键值 | BlackboardSerializer |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `BlackboardSerializer_Serialize_EmptyBlackboard_ReturnsValidJson` | 空白板序列化 | 返回合法 JSON（包含 `{`） |

## SndSceneSerializerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndSceneSerializer_Serialize_EmptyScene` | 空场景序列化返回 JSON 数组 | SndSceneSerializer |
| `SndSceneSerializer_RoundTrip_PreservesMetaList` | 场景实体序列化后反序列化恢复实体名 | SndSceneSerializer |
| `SndSceneSerializer_DeserializeInto_ClearsBeforeLoad` | 反序列化前 SceneHost 被清空（ClearAllCount = 0 验证） | SndSceneSerializer |
| `SndSceneSerializer_DeserializeInto_NoClearWhenFalse` | 反复调用反序列化不会多次清空 | SndSceneSerializer |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SndSceneSerializer_DeserializeInto_InvalidJson_Throws` | 非法 JSON（对象而非数组） | 抛出异常 |
| `SndSceneSerializer_Constructor_ThrowsOnNullWorld` | null SndWorld | ArgumentNullException |

## SaveContextTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SaveContext_SerializeProgress_And_DeserializeProgress_RoundTrip` | Progress 黑板序列化→反序列化数据一致 | SaveContext |
| `SaveContext_SerializeSession_And_DeserializeSession_RoundTrip` | Session 黑板序列化→反序列化数据一致 | SaveContext |
| `SaveContext_SerializeSndScene_ReturnsJson` | BuildSndScene 返回非 null DataSourceNode | SaveContext |
| `SaveContext_RecoverSndScene_LoadsEntities` | 从 JSON 恢复实体到 SceneHost | SaveContext |
| `SaveContext_SaveGame_CreatesSaveGamePayload` | SaveGame 构建完整 Payload 含 SaveId/ActiveLevelId/Levels | SaveContext |
| `SaveContext_SaveGame_WithCustomMeta` | SaveGame 携带 CustomMeta 字典 | SaveContext |
| `SaveContext_Properties_ExposeBlackboards` | Progress/Session/SndWorld 属性引用一致性 | SaveContext |
| `DeserializeProgress_ThenVerify_BlackboardDataUpdated` | DeserializeProgress 后 Progress 黑板键值正确更新（含新加入的键） | SaveContext |
| `DeserializeSession_ThenVerify_BlackboardDataUpdated` | DeserializeSession 后 Session 黑板键值正确更新（含新加入的键） | SaveContext |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SaveContext_Constructor_ThrowsOnNullArgs` | 任意构造参数为 null（Progress/Session/SndWorld） | ArgumentNullException |
| `DeserializeProgress_NullNode_ThrowsArgumentNullException` | null DataSourceNode | ArgumentNullException |

## SaveCoordinatorTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SaveContext_Constructor_ThrowsOnNullArgs` | null progress / null session / null sndWorld 参数 | ArgumentNullException |
| `PersistProgress_WithoutForegroundSession_Throws` | PersistProgress 在无前台 Session 时调用 | InvalidOperationException（消息含 "foreground"） |

## PersistentBlackboardTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `PersistentBlackboard_SetAndLoadFromDisk_Works` | SetValue → 重载 → LoadFromDisk 恢复键值 | PersistentBlackboard |
| `PersistentBlackboard_Clear_PersistsEmptyData` | Clear 后磁盘上的数据为空 Map | PersistentBlackboard |
| `PersistentBlackboard_WriteUsesTempAndRename` | SetValue 写盘走 `.tmp.json` 临时文件 + rename 原子写入，成功后无临时残留 | PersistentBlackboard |
| `PersistentBlackboard_UpdatedValue_OverwritesViaAtomicRename` | 更新同一键后重载读到最新值（原子覆写） | PersistentBlackboard |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `PersistentBlackboard_StaleTempFile_CleanedUpOnLoad` | 存在陈旧 `.tmp.json` 残留 | 加载时删除临时文件 |
| `PersistentBlackboard_SuccessfulWrite_LeavesNoBackupFile` | 覆写成功后 | 无 `.bak.json` 残留 |
| `PersistentBlackboard_LoadFromDisk_RecoversPreviousVersionFromBackup` | 主文件缺失、备份存有旧版本（模拟崩溃） | 从备份恢复主文件并消费备份 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `TestStateMachineContext` | SaveCoordinatorTests.cs | IStateMachineContext 桩，提供空黑板和空 SceneAccess |
| `TestSceneAccess` | SaveCoordinatorTests.cs | ISndSceneAccess 桩，返回空实体列表 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| BeforeSave 钩子触发后数据未正确刷新到 Payload | 序列化前钩子未执行 | snd-entity-model |
| BlackboardSerializer 对复杂自定义类型的 TypedData 往返 | 当前仅测试 int/string，未测试嵌套对象/数组 | BlackboardSerializer |
| SndSceneSerializer 对含策略元数据的实体序列化 | 当前仅测试基础 Name 字段 | SndSceneSerializer |

## 设计决策

### SaveContext 反序列化原子回滚

`SaveContext.DeserializeProgress()` 和 `DeserializeSession()` 在反序列化前对目标黑板做快照。若 `DeserializeInto()` 抛出异常，黑板被恢复到快照状态，确保反序列化失败不会导致黑板部分被修改。

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
