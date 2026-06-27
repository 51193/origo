# 持久化：序列化 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Save/Serialization](../Origo.Core/Save/Serialization/README.md)
> [↔ 被测行为: usage/persistence-flow](../usage/persistence-flow.md)

## 被测行为概览

验证存档 Payload 的序列化编排：Blackboard 序列化/反序列化往返、SND 场景实体列表序列化、
SaveContext 在 ProgressRun 流程中的协调行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `BlackboardSerializerTests.cs` | Blackboard 序列化/反序列化：TypedData 类型保留、多键值往返 |
| `SndSceneSerializerTests.cs` | SND 场景序列化：实体元数据列表 ↔ DataSourceNode 往返 |
| `SaveContextTests.cs` | SaveContext：Payload 构建/写入编排与 Session 数据收集 |

## BlackboardSerializerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Serialize_ThenDeserialize_PreservesAllKeys` | 序列化后反序列化恢复全部键值，类型不丢失 | Blackboard Abstraction |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Serialize_EmptyBlackboard_ProducesEmptyDict` | 空白板序列化 | 空字典 |

## SndSceneSerializerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Build_ThenRecover_PreservesEntityNames` | 场景实体列表序列化后恢复实体名 | Snd/Scene |
| `Recover_IntoHost_RestoresEntities` | 反序列化后 SceneHost 恢复实体列表 | Snd/Scene |

## SaveContextTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SaveContext_SerializeProgress_And_DeserializeProgress_RoundTrip` | 进度黑板序列化→反序列化数据一致 | Save/README.md |
| `SaveContext_SerializeSession_And_DeserializeSession_RoundTrip` | 会话黑板序列化→反序列化数据一致 | Save/README.md |
| 测试 SaveContext 在 ProgressRun 流程中构建 Payload 并写入 | SaveContext 编排黑板的 Collection + 写入 | Save/README.md |
| `DeserializeProgress_ThenVerify_BlackboardDataUpdated` | DeserializeProgress 后黑板数据正确更新 | Save/README.md |
| `DeserializeSession_ThenVerify_BlackboardDataUpdated` | DeserializeSession 后黑板数据正确更新 | Save/README.md |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `DeserializeProgress_NullNode_ThrowsArgumentNullException` | null DataSourceNode | ArgumentNullException |

### 设计决策

#### SaveContext 反序列化原子回滚

`SaveContext.DeserializeProgress()` 和 `DeserializeSession()` 在反序列化前对目标黑板做快照。若 `DeserializeInto()` 抛出异常，黑板被恢复到快照状态，确保反序列化失败不会导致黑板部分被修改。

实现流程：
1. 调用 `blackboard.SerializeAll()` 生成快照字典
2. 在 `try` 块中执行 `DeserializeInto`
3. 若异常：调用 `blackboard.DeserializeAll(snapshot)` 恢复原始状态，然后重新抛出异常

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无（本组测试直接使用 TestSndSceneHost 和 TestFileSystem） | — | — |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| BeforeSave 钩子触发后数据未正确刷新到 Payload | 序列化前钩子未执行 | snd-entity-model |

---

[↑ 回到 Origo.Core.Tests](README.md)
