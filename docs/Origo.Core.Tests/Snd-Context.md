# SND 上下文 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd](../Origo.Core/Snd/README.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.md)

## 被测行为概览

验证 SndContext 作为 SND 系统的核心编排器的全部工作流：save/load/continue 操作、
控制台命令提交、模板克隆、延迟动作队列、NullSndContext 的无操作行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `SndContextWorkflowTests.cs` | SndContext save/load/continue/switch 全链路工作流 |
| `SndContextEntryFlowTests.cs` | SndContext 从入口配置开始的工作流 |
| `NullSndContextExtendedTests.cs` | NullSndContext 所有方法为无操作 |
| `SessionSndContextExtendedTests.cs` | SessionSndContext 隔离上下文行为 |
| `LevelBuilderExtendedTests.cs` | LevelBuilder 构建和写入关卡数据 |
| `SndWorldAndDiscoveryCoverageTests.cs` | SndWorld 策略发现和模板加载 |
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
| `InitialState_NoProgressBlackboard_NoCurrentSession` | 刚创建时无 Progress 和 CurrentSession | null |
| `RequestSaveGame_ConcurrentWorkflow_AllowsSequentialSavesInSingleFlush` | 同一 Flush 中多次 Save | 不抛异常 |

## SndTemplateResolverTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Resolve_WhenCalledTwice_UsesCacheAndAvoidsSecondRead` | 第二次 Resolve 使用缓存，不重复读文件 | SndTemplateResolver |
| `Resolve_CacheThenClone_CloneDoesNotAffectCache` | DeepClone 不污染缓存 | SndTemplateResolver |
| `Resolve_TemplateFile_EmptyObject_ReturnsMinimalMetaData` | 空 JSON → Name 为空串的 MetaData | — |
| `Resolve_TemplateFile_MissingNameField_ReturnsEmptyName` | 无 name 字段时返回 Name 为空的 MetaData | — |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Resolve_MissingAlias_ThrowsKeyNotFoundException` | 不存在的别名 | KeyNotFoundException |
| `Resolve_WhitespaceAlias_ThrowsArgumentException` | 空白别名 | ArgumentException |
| `Resolve_InvalidJson_Throws` | 无效 JSON 模板文件 | Exception |
| `Resolve_ConverterReturnsNull_ThrowsInvalidOperationException` | 转换器返回 null | InvalidOperationException（含 "deserialized to null"） |

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

[↑ 回到 Origo.Core.Tests](README.md)
