<!-- docsync-pair: Origo.Core.Tests/Snd-Entity -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# SND 实体 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Snd/Entity](../Origo.Core/Snd/Entity/README.zh.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.zh.md)

## 被测行为概览

验证 SND 实体的完整行为：StubSndEntity 的数据 CRUD、AfterLoad 钩子触发时机与顺序、
AutoInitializer 的策略/数据恢复、批量生命周期编排（AfterLoad/AfterSpawn/BeforeSave/BeforeQuit/BeforeDead）、
实体与 OwningSession 的绑定关系、SndEntityFactory 的 spawn 编排、ProcessAll 帧处理行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `MemorySndEntityTests.cs` | SndEntity 的 SetData/GetData/TryGetData/数据隔离 |
| `SndEntityAfterLoadTests.cs` | AfterLoad 钩子的触发顺序和错误传播 |
| `SndEntityAndAutoInitializerTests.cs` | AutoInitializer 从 metadata 恢复策略和数据；SndEntity AddStrategy/RemoveStrategy 索引更新 |
| `SndEntityLifecycleBatchTests.cs` | 批量生命周期编排：全部钩子阶段、跨实体查找、优先级、SndEntityFactory/Spawn、ProcessAll 帧处理 |
| `SndEntityOwningSessionTests.cs` | 实体 OwningSession 绑定与解除 |

## MemorySndEntityTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Name_ReturnsConstructedName` | 构造函数指定的名称可通过 Name 属性获取 | ISndEntity |
| `SetData_GetData_RoundTrip` | SetData/GetData 往返保持值一致 | snd-entity-model: TypedData |
| `TryGetData_ReturnsTrueWhenFound` | TryGetData 在键存在时返回 true 和值 | snd-entity-model: TypedData |
| `InitialNameData_IsSetInDictionary` | 构造时名称自动存入 "name" 数据条目 | ISndEntity |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Constructor_ThrowsOnNullName` | null 名称参数 | ArgumentNullException |
| `GetData_ThrowsKeyNotFound_WhenMissing` | 不存在的键 | KeyNotFoundException |
| `GetData_ThrowsInvalidCast_OnTypeMismatch` | 类型不匹配 | InvalidCastException |
| `GetNode_ThrowsInvalidOperation` | Stub 实体不支持节点操作 | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TryGetData_ReturnsFalseWhenMissing` | 键不存在 | 返回 false |
| `TryGetData_ReturnsFalseForTypeMismatch` | 类型不匹配 | 返回 false，不抛异常 |
| `GetNodeNames_ReturnsEmpty` | 无节点实体 | 返回空集合 |
| `AddRemoveStrategy_DoesNotThrow` | Stub 实体的 AddStrategy/RemoveStrategy | 无操作，不抛异常，已有数据不受影响 |

## SndEntityAfterLoadTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndEntity_Load_FromJson_InvokesAfterLoad_ForAllStrategies_InIndexOrder` | AfterLoad 钩子按 metadata 索引顺序依次触发所有策略 | snd-entity-model: 生命周期钩子 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `AfterLoad_ThrowingStrategy_HookExceptionPropagates` | 策略 AfterLoad 抛出 InvalidOperationException | InvalidOperationException 传播 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `AfterLoad_EmptyIndices_NoThrow` | 空 lifecycle_indices | 不抛异常，实体正常可用 |

## SndEntityAndAutoInitializerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndEntity_GetNodeNamesAndGetNode_ReturnExpectedHandles` | Spawn 后 GetNodeNames/GetNode 返回 metadata 中定义的节点句柄 | ISndEntity |
| `SndEntity_AddRemoveStrategy_UpdatesExportedIndices` | AddStrategy/RemoveStrategy 正确更新导出的 lifecycle_indices；重复 RemoveStrategy 不抛异常 | ISndEntity |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_LoadsInlineMetaArray` | 从 JSON 数组文件加载并批量 spawn 实体 | Snd/Entity: AutoInitializer |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SndEntity_GetData_MissingKey_ThrowsInvalidOperation` | GetData 访问不存在的键 | InvalidOperationException |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyPath_Throws` | 空白路径字符串 | ArgumentException，日志记录错误 |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_MissingFile_Throws` | 文件不存在 | InvalidOperationException |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_EmptyFile_Throws` | 文件内容为空/空白 | 抛异常 |
| `OrigoAutoInitializer_LoadAndSpawnFromFile_NotArrayRoot_Throws` | JSON 根节点不是数组 | InvalidOperationException |

## SndEntityLifecycleBatchTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `BatchLoad_AfterLoad_FiresAfterAllEntitiesRecovered` | AfterLoad 钩子在所有实体 RecoverFromMetaList 完成后统一触发 | snd-entity-model: 批量生命周期 |
| `BatchLoad_CrossEntity_FindByName_SucceedsRegardlessOfOrder` | AfterLoad 期间 FindByName 可找到所有实体，不受恢复顺序影响 | snd-entity-model: 批量生命周期 |
| `BatchLoad_Self_ActiveStrategyAvailableDuringAfterLoad` | AfterLoad 期间自身 ActiveStrategy 可用 | snd-entity-model: 批量生命周期 |
| `BatchLoad_CrossEntity_ActiveStrategyAvailableDuringAfterLoad` | AfterLoad 期间可通过 InvokeStrategy 调用其他实体的 ActiveStrategy | snd-entity-model: 批量生命周期 |
| `BatchLoad_CrossEntity_SubscribeDuringAfterLoad` | AfterLoad 期间可跨实体订阅数据观察 | snd-entity-model: 批量生命周期 |
| `SpawnMany_AfterSpawn_FiresOnAllEntities` | AfterSpawn 钩子在所有实体创建后触发 | snd-entity-model: 批量生命周期 |
| `SpawnMany_CrossEntity_ActiveStrategyAvailableDuringAfterSpawn` | AfterSpawn 期间可跨实体调用 ActiveStrategy | snd-entity-model: 批量生命周期 |
| `BatchSave_BeforeSave_FiresBeforeAnySerialization` | BeforeSave 钩子在 BuildMetaList 之前触发 | snd-entity-model: 批量生命周期 |
| `BatchQuit_BeforeQuit_FiresBeforeAnyTeardown` | BeforeQuit 在拆卸前触发，之后 ReleaseStrategiesOnly + RemoveAllEntities | snd-entity-model: 批量生命周期 |
| `BatchQuit_LifoOrder_Preserved` | BeforeQuit 按 LIFO 顺序触发 | snd-entity-model: 批量生命周期 |
| `BatchQuit_CrossEntity_FindByNameSucceedsDuringBeforeQuit` | BeforeQuit 期间 FindByName 仍可找到其他实体 | snd-entity-model: 批量生命周期 |
| `BatchDead_BeforeDead_FiresBeforeAnyTeardown` | BeforeDead 在 RemoveEntity 前触发 | snd-entity-model: 批量生命周期 |
| `BatchDead_CrossEntity_FindByNameSucceedsDuringBeforeDead` | BeforeDead 期间 FindByName 仍可找到其他实体 | snd-entity-model: 批量生命周期 |
| `BatchLoad_StrategyPriorityWithinEntity_Preserved` | 同一实体多个策略按 Priority 排序（低优先在前） | snd-entity-model: 策略优先级 |
| `BatchLoad_SingleEntity_BehaviorCorrect` | 单实体批量恢复正确触发 AfterLoad | snd-entity-model: 批量生命周期 |
| `SpawnSingle_ActiveStrategyAvailableDuringAfterSpawn` | 单实体 Spawn 后 AfterSpawn 期间 ActiveStrategy 可用 | snd-entity-model: 批量生命周期 |
| `LoadSingle_ActiveStrategyAvailableDuringAfterLoad` | 单实体 Load 后 AfterLoad 期间 ActiveStrategy 可用 | snd-entity-model: 批量生命周期 |
| `SndEntityFactory_SpawnMany_TriggersAfterSpawnAfterAllCreated` | SpawnMany 在全部实体创建后统一触发 AfterSpawn | SndEntityFactory |
| `SndEntityFactory_Spawn_CallsCreateEntityThenFiresAfterSpawn` | Spawn 先 CreateEntity 再触发 AfterSpawn | SndEntityFactory |
| `SndEntityFactory_SpawnMany_EntitiesVisibleInAfterSpawn` | SpawnMany 的 AfterSpawn 钩子中所有实体可见 | SndEntityFactory |
| `ProcessAll_SingleEntity_CallsProcessOnStrategy` | ProcessAll 调用策略 Process，delta 正确传播 | snd-entity-model: 帧处理 |
| `ProcessAll_MultipleEntities_AllProcessed` | 多实体帧处理全部执行 | snd-entity-model: 帧处理 |
| `ProcessAll_DeltaPropagatesToStrategy` | ProcessAll 的 delta 参数正确传递给策略 Process | snd-entity-model: 帧处理 |
| `ProcessAll_ProcessAddsStrategy_NewStrategyNotExecutedThisFrame` | Process 中 AddStrategy 的新策略当前帧不执行 | snd-entity-model: 帧处理 |
| `ProcessAll_ProcessRemovesStrategy_RemainingStrategiesStillExecuted` | Process 中 RemoveStrategy 后后续策略仍正常执行 | snd-entity-model: 帧处理 |
| `SndEntityFactory_Spawn_CreatesEntityAndFiresAfterSpawn` | Spawn 创建实体并触发 AfterSpawn 钩子 | SndEntityFactory |
| `SndEntityFactory_SpawnMany_BatchCreatesAllThenFiresHooks` | SpawnMany 全部创建后逐个触发钩子 | SndEntityFactory |
| `SndEntityFactory_SpawnMany_EntitiesVisibleDuringAfterSpawn` | SpawnMany AfterSpawn 期间跨实体 FindByName 可见 | SndEntityFactory |
| `FullMemorySndSceneHost_RemoveEntity_ClearsCollectionOnly` | RemoveEntity 仅从集合移除；重复 Remove 抛异常 | ISndSceneHost |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `BatchLoad_HookThrows_EntitiesCleanedUp` | AfterLoad 钩子抛出 InvalidOperationException | 异常传播，实体已被清理 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `BatchLoad_EmptyList_DoesNothing` | 空列表传给 RecoverFromMetaList | 不抛异常，实体列表为空 |
| `CreateEntity_DoesNotFireAfterSpawnHooks` | 直接调用 CreateEntity | AfterSpawn 钩子不触发 |
| `RemoveEntity_DoesNotFireBeforeDeadHooks` | 直接调用 RemoveEntity | BeforeDead 钩子不触发 |
| `SndEntityFactory_Spawn_WithNonLifecycleEntity_DoesNotThrow` | 非 IEntityLifecycle 实体 | 不抛异常，正常返回 |
| `SndEntityFactory_SpawnMany_WithNonLifecycleEntity_DoesNotThrow` | 多个非 IEntityLifecycle 实体 | 不抛异常，全部创建 |
| `ProcessAll_DoesNotThrowForEmptyScene` | 无实体的场景调用 ProcessAll | 不抛异常 |

## SndEntityOwningSessionTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateEntity_WithOwningSession_BindsOwningSessionToEntity` | 通过 IOwningSessionBindable.SetOwningSession 绑定后实体 OwningSession 可访问 | ISndEntity |
| `SndEntityFactory_Spawn_CreatesEntityAndFiresAfterSpawnHooks` | Spawn 创建实体并触发 AfterSpawn 钩子 | SndEntityFactory |
| `SndEntityFactory_SpawnMany_CreatesMultipleEntitiesAndFiresHooks` | SpawnMany 创建多个实体并触发所有 AfterSpawn 钩子 | SndEntityFactory |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `CreateEntity_WithoutOwningSession_OwningSessionThrows` | 未绑定 OwningSession 时访问 OwningSession | InvalidOperationException |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `ProbeStrategy` | SndEntityLifecycleBatchTests | AsyncLocal 事件记录，验证各生命周期钩子（AfterLoad/AfterSpawn/BeforeSave/BeforeQuit/BeforeDead）被调用的次数和顺序 |
| `CrossRefStrategy` | SndEntityLifecycleBatchTests | 跨实体 FindByName 验证：在 AfterLoad/AfterSpawn/BeforeQuit/BeforeDead 中验证是否可找到指定名称的其他实体 |
| `QueryActiveProxy` | SndEntityLifecycleBatchTests | 跨实体 InvokeStrategy 验证：验证 AfterLoad/AfterSpawn 期间可通过 ActiveStrategy 调用其他实体 |
| `SimpleActiveStrategy` | SndEntityLifecycleBatchTests | Active 策略，Invoke 返回 `hello_from:{entity.Name}` 字符串 |
| `SP50` / `SP100` | SndEntityLifecycleBatchTests | 优先级验证：Priority=50 和 Priority=100 两个策略共享事件收集器，验证低优先先执行 |
| `FailingStrategy` | SndEntityLifecycleBatchTests | Always-throws：AfterLoad 始终抛出 InvalidOperationException，用于错误路径测试 |
| `SubscribeStrategy` | SndEntityLifecycleBatchTests | 数据订阅测试：AfterLoad 中跨实体订阅数据变化并通过 AsyncLocal 记录通知事件 |
| `ProcessRecordingStrategy` | SndEntityLifecycleBatchTests | 记录 Process 调用的 (entity.Name, delta) 元组 |
| `AddDuringProcessStrategy` | SndEntityLifecycleBatchTests | Process 中动态 AddStrategy，验证新策略本帧不执行 |
| `SelfRemoveRecordingStrategy` | SndEntityLifecycleBatchTests | 记录 self_remove 事件 |
| `RemoveSelfDuringProcessStrategy` | SndEntityLifecycleBatchTests | Process 中动态 RemoveStrategy 自身，验证后续策略仍执行 |
| `AfterLoadProbeAStrategy` / `AfterLoadProbeBStrategy` | SndEntityAfterLoadTests | AsyncLocal 共享事件列表，验证 AfterLoad 按索引顺序触发的顺序 |
| `ThrowingAfterLoadStrategy` | SndEntityAfterLoadTests | AfterLoad 抛出 InvalidOperationException，验证异常传播 |
| `LifecycleStrategy` | SndEntityAndAutoInitializerTests | AsyncLocal 事件收集器，覆盖全部生命周期钩子（AfterSpawn/AfterAdd/BeforeRemove/BeforeSave/BeforeQuit） |
| `StubSessionRun` | SndEntityAndAutoInitializerTests | ISessionRun 桩实现，将 Spawn/FindByName/GetEntities 委托给 ISndSceneHost |
| `AutoInitStrategyA` / `AutoInitStrategyB` | SndEntityAndAutoInitializerTests | 最小 LifecycleStrategyBase 策略，仅声明 StrategyIndex，无行为覆盖 |
| `StatefulAutoInitStrategy` | SndEntityAndAutoInitializerTests | 带实例字段 (_counter) 的策略，用于测试框架对非无状态策略的守卫 |
| `TrackingStrategy` | SndEntityOwningSessionTests | 通过构造函数注入 List<string> 记录 AfterSpawn 事件 |
| `StubSessionRun` | SndEntityOwningSessionTests | 最小 ISessionRun 桩，所有操作抛出 NotSupportedException |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| AutoInitializer 恢复时 metadata 类型不匹配 | 损坏的 metadata 数据导致的数据损坏场景 | Snd/Entity |
| AfterLoad 在策略已存在时增量添加 | AfterLoad 后动态 AddStrategy 的钩子行为 | snd-entity-model |
| 大量实体并发 ProcessAll 的帧时间稳定性 | 极端实体数量的性能特征 | snd-entity-model: 帧处理 |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
