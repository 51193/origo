<!-- docsync-pair: Origo.Core.Tests/Snd-Context -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# SND 上下文 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Snd](../Origo.Core/Snd/README.zh.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.zh.md)

## 被测行为概览

验证 SndContext 作为 SND 系统的核心编排器的全部工作流：save/load/continue 操作、
控制台命令提交、模板克隆、延迟动作队列、NullSndContext 的无操作行为、
LevelBuilder 关卡构建、Archetype 加载与属性解析、入口配置启动流程、
模板别名解析与缓存。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `SndContextWorkflowTests.cs` | SndContext save/load/continue/switch 全链路工作流 |
| `SndContextEntryFlowTests.cs` | SndContext 从入口配置开始的工作流 |
| `SndContextBootstrapTests.cs` | Bootstrap 启动流程：策略发现、别名/模板加载、入口存档加载的顺序与配置开关 |
| `LevelBuilderExtendedTests.cs` | LevelBuilder 构建和写入关卡数据 |
| `SndArchetypeLoaderTests.cs` | SndArchetypeLoader.TryLoad 解析与 ApplyAttributes 类型推断 |
| `SndTemplateResolverTests.cs` | 模板别名解析、缓存、克隆不影响缓存 |

## SndContextWorkflowTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ListSaves_ReturnsEmptyWhenNoSaves` | 无存档时 ListSaves 返回空 | ISndSaveOperations |
| `ListSaves_ReturnsSaveIds` | 有存档时 ListSaves 返回存档 ID | ISndSaveOperations |
| `RequestSaveGame_PersistsAndSetsActiveSaveSlot` | 保存后文件存在、ActiveSaveId 正确设置 | persistence-flow |
| `RequestSaveGame_IncrementsThenDecrementsPendingCount` | 保存请求先增后减 pending 计数 | ISndDeferredActions |
| `RequestSaveGameAuto_WithExplicitId_UsesIt` | RequestSaveGameAuto 使用传入 ID | ISndSaveOperations |
| `RequestSaveGameAuto_WithNullId_GeneratesTimestamp` | 未传 ID 时自动生成时间戳 | ISndSaveOperations |
| `RequestLoadGame_LoadsSaveAndRestoresProgress` | LoadGame 后 ProgressBlackboard 和 ForegroundSession 可用 | ISndSaveOperations |
| `RequestLoadGame_IncrementsThenDecrementsPendingCount` | Load 请求的 pending 计数变化 | ISndDeferredActions |
| `SetContinueTarget_MakesHasContinueDataTrue` | 设置 Continue 目标后 HasContinueData 返回 true | ISndLifecycleOperations |
| `RequestContinueGame_ReturnsTrueAndLoadsWhenContinueSet` | Continue 正确加载存档 | ISndLifecycleOperations |
| `RequestLoadInitialSave_LoadsFromInitialRoot` | 从初始路径加载初始存档 | ISndLifecycleOperations |
| `RequestSwitchForegroundLevel_SwitchesLevel` | 关卡切换后 ForegroundSession.LevelId 正确 | ISndLifecycleOperations |
| `CloneTemplate_ClonesAndOverridesName` | 克隆模板并覆盖名字 | ISndTemplateAccess |
| `CloneTemplate_WithoutOverrideName_KeepsOriginal` | 不覆盖名字时保留原名 | ISndTemplateAccess |
| `TrySubmitConsoleCommand_ReturnsTrueWhenConsoleInputExists` | 有控制台输入时提交命令成功 | ISndConsoleAccess |
| `ProcessConsolePending_ProcessesQueuedCommands` | ProcessConsolePending 处理排队命令 | ISndConsoleAccess |
| `SubscribeConsoleOutput_ReturnsPositiveId` | 订阅返回正数 ID | ISndConsoleAccess |
| `UnsubscribeConsoleOutput_RemovesSubscription` | 取消订阅后不再收到消息 | ISndConsoleAccess |
| `EnqueueBusinessDeferred_ExecutesOnFlush` | 延迟动作在 Flush 时执行 | ISndDeferredActions |
| `GetPendingPersistenceRequestCount_InitiallyZero` | 初始 pending 计数为 0 | ISndDeferredActions |
| `GetProgressStateMachines_NullWhenNoProgress` | 无 ProgressRun 时状态机容器为 null | ISndStateMachineAccess |
| `GetProgressStateMachines_NotNullAfterProgressRunCreated` | 有 ProgressRun 后状态机容器可用 | ISndStateMachineAccess |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `RequestSaveGame_ThrowsOnEmptyId` | 空 saveId | ArgumentException |
| `RequestSaveGame_ThrowsOnNullId` | null saveId | ArgumentException |
| `RequestLoadGame_ThrowsOnEmptyId` | 空 saveId | ArgumentException |
| `RequestLoadGame_ThrowsOnNullId` | null saveId | ArgumentException |
| `RequestSwitchForegroundLevel_ThrowsOnEmptyId` | 空 levelId | ArgumentException |
| `TrySubmitConsoleCommand_ReturnsFalseForEmptyCommand` | 空白命令 | 返回 false |
| `TrySubmitConsoleCommand_ReturnsFalseWhenNoConsoleInput` | 无控制台输入源 | 返回 false |
| `SubscribeConsoleOutput_ThrowsWhenNoChannel` | 无输出通道时订阅 | InvalidOperationException |
| `RequestContinueGame_ReturnsFalseWhenNoContinue` | 未设置 Continue 目标 | 返回 false |
| `Constructor_ThrowsOnNullRuntime` | null Runtime | ArgumentNullException |
| `Constructor_ThrowsOnNullFileSystem` | null FileSystem | ArgumentNullException |
| `Constructor_ThrowsOnEmptySaveRootPath` | 空白 SaveRootPath | ArgumentException |
| `Constructor_ThrowsOnEmptyInitialSaveRootPath` | 空白 InitialSaveRootPath | ArgumentException |
| `Constructor_ThrowsOnEmptyEntryConfigPath` | 空白 EntryConfigPath | ArgumentException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `HasContinueData_FalseWhenNoTargetSet` | 未设置 Continue 目标 | 返回 false |
| `InitialState_NoProgressBlackboard_NoForegroundSession` | 刚创建时无 Progress 和前台会话（ForegroundSession 为 null） | null |
| `RequestSaveGame_ConcurrentWorkflow_AllowsSequentialSavesInSingleFlush` | 同一 Flush 中多次 Save | 不抛异常 |

## SndTemplateResolverTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Resolve_WhenCalledTwice_UsesCacheAndAvoidsSecondRead` | 第二次 Resolve 使用缓存，不重复读文件 | SndTemplateResolver |
| `Resolve_CacheThenClone_CloneDoesNotAffectCache` | DeepClone 不污染缓存 | SndTemplateResolver |
| `Resolve_TemplateFile_EmptyObject_ReturnsMinimalMetaData` | 空 JSON → Name 为空串的 MetaData | — |
| `Resolve_TemplateFile_MissingNameField_ReturnsEmptyName` | 无 name 字段时返回 Name 为空的 MetaData | — |
| `Resolve_MapFileComments_Skipped` | 模板文件正常解析，名称正确返回 | SndTemplateResolver |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Resolve_MissingAlias_ThrowsKeyNotFoundException` | 不存在的别名 | KeyNotFoundException |
| `Resolve_WhitespaceAlias_ThrowsArgumentException` | 空白别名 | ArgumentException |
| `Resolve_InvalidJson_Throws` | 无效 JSON 模板文件 | Exception |
| `Resolve_ConverterReturnsNull_ThrowsInvalidOperationException` | 转换器返回 null | InvalidOperationException（含 "deserialized to null"） |

## NullSndContext（测试基础设施）

`NullSndContext` 已从生产代码（`Origo.Core/Snd/`）迁移到测试项目（`Origo.Core.Tests/TestSupport/`），作为测试辅助类使用。
自引用测试文件 `NullSndContextExtendedTests.cs` 已随迁移删除。

## LevelBuilderExtendedTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Build_ProducesLevelPayload` | Build() 产生包含 LevelId、SndSceneNode、SessionNode、SessionStateMachinesNode 的有效负载 | LevelBuilder |
| `Commit_WritesToFileSystem` | Commit() 写入 payload 到文件系统 `root/current/level_lvl1/snd_scene.json` | LevelBuilder |
| `AddEntities_BatchAdd` | AddEntities 批量添加 3 个实体，SceneHost 实体计数为 3 | LevelBuilder |
| `AddEntityFromTemplate_ClonesAndAdds` | AddEntityFromTemplate 克隆模板并通过 SceneHost.FindByName 可找到 | LevelBuilder |
| `SessionBlackboard_IsAccessible` | SetSessionData 写入后 SessionBlackboard.TryGet 可读回 | SessionBlackboard |
| `LevelId_ExposesConstructedValue` | 构造时传入的 LevelId 通过 LevelId 属性暴露 | LevelBuilder |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Build_ThenModify_Throws` | Build 后调用 AddEntity / SetSessionData / Build | InvalidOperationException |
| `AddEntity_DuplicateName_Throws` | 重复名称 AddEntity | InvalidOperationException |
| `AddEntity_NullMeta_Throws` | null SndMetaData | ArgumentNullException |
| `AddEntity_EmptyName_Throws` | Name 为空串的 SndMetaData | ArgumentException |
| `AddEntities_NullList_Throws` | null 实体列表 | ArgumentNullException |
| `AddEntityFromTemplate_EmptyKey_Throws` | 空模板 key | ArgumentException |
| `Constructor_EmptyLevelId_Throws` | 空 levelId | ArgumentException |
| `Constructor_NullSndWorld_Throws` | null SndWorld | ArgumentNullException |
| `Constructor_NullStorage_Throws` | null ISaveStorageService | ArgumentNullException |

## SndContextEntryFlowTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RequestLoadMainMenuEntrySave_MountsForegroundAndSpawnsEntryEntities` | 加载入口存档后 ProgressBlackboard 非 null、ForegroundSession 非 null、host 中可找到入口实体 | ISndLifecycleOperations |
| `RequestLoadMainMenuEntrySave_ClearsPreviousForegroundEntities` | 加载入口存档前遗留的实体在加载后被清除 | ISndLifecycleOperations |

## SndContextBootstrapTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Bootstrap_CompletesWithoutError` | 提供 entry.json 时 Bootstrap 完整执行无异常 | ISndContext.Bootstrap |
| `Bootstrap_AfterCall_ForegroundSessionIsEstablished` | Bootstrap 后冲刷延迟队列，前台会话已挂载 | ISndContext.Bootstrap |
| `Bootstrap_WithConfigureConverters_CallbackIsInvoked` | ConfigureConverters 回调在策略发现前被调用 | ISndContext.Bootstrap |
| `Bootstrap_AutoDiscoverDisabled_SkipsStrategyDiscovery` | AutoDiscoverStrategies=false 时跳过策略扫描 | SndContextParameters.AutoDiscoverStrategies |
| `Bootstrap_WithTemplates_LoadsAndAllowsCloning` | 配置模板路径后可 CloneTemplate | SndWorld.LoadTemplates |
| `IStateMachineContext_SceneAccess_AfterBootstrap_NotNull` | Bootstrap 后状态机上下文 SceneAccess 可用 | IStateMachineContext |
| `IStateMachineContext_SystemBlackboard_AfterBootstrap_NotNull` | Bootstrap 后系统黑板可访问 | IStateMachineContext |
| `IStateMachineContext_ProgressBlackboard_AfterBootstrap_NotNull` | Bootstrap 后流程黑板可访问 | IStateMachineContext |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Bootstrap_WithoutEntryJson_ThrowsOnFlush` | 缺少 entry.json | 冲刷延迟队列时抛出异常（fail-fast） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SaveRootPath_ReturnsConstructorValue` | 构造参数 | 返回构造时传入的存档根路径 |
| `InitialSaveRootPath_ReturnsConstructorValue` | 构造参数 | 返回初始存档根路径 |
| `EntryConfigPath_ReturnsConstructorValue` | 构造参数 | 返回入口配置路径 |

## SndArchetypeLoaderTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `TryLoad_ValidMapFile_ReturnsAttributes` | 有效 map 文件解析返回 4 个属性，键值正确 | SndArchetypeLoader.TryLoad |
| `ApplyAttributes_IntString_StoresAsInt` | 整数字符串 "100" 存储为 int(100) | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_LargeIntegerString_StoresAsLong` | 超大整数字符串超过 int.MaxValue 时存储为 long，不存为 float | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_FloatString_StoresAsFloat` | 浮点字符串 "3.14" 存储为 float(3.14f) | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_BoolString_StoresAsBool` | "true" 存储为 bool(true) | SndArchetypeLoader.ApplyAttributes |
| `ApplyAttributes_PlainString_StoresAsString` | 普通字符串 "hero" 存储为 string | SndArchetypeLoader.ApplyAttributes |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TryLoad_FileNotExists_ReturnsFalse` | 文件不存在 | 返回 false，attrs 为空 |
| `TryLoad_EmptyObject_ReturnsFalse` | 空 JSON 对象 {} | 返回 false，attrs 为空 |
| `TryLoad_NonObjectNode_ReturnsFalse` | JSON 值为字符串而非对象 | 返回 false，attrs 为空 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `NullMetaConverter` | SndTemplateResolverTests.cs | 返回 null 的转换器，验证 null 检测 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| RequestSaveGame 在无 ProgressRun 时的行为 | 未设置 ProgressRun 时 Save 应如何处理 | ISndSaveOperations |
| SndContext 并发调用 FlushDeferredActions | 多线程 Flush 的线程安全 | — |
| CloneTemplate 传入空 overrideName 的行为 | 空名字覆盖 | ISndTemplateAccess |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
