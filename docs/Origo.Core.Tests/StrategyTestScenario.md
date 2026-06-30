# 策略测试框架 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测行为: usage/strategy-testing](../usage/strategy-testing.md)

## 被测行为概览

验证 StrategyTestScenario 测试框架本身的正确性。该框架位于 `Origo.Core.Tests/TestSupport/`，
是测试基础设施的一部分（在 [Tests README](README.md) 中有概述），没有对应的生产模块。

确保框架的 Harness 能正确模拟 EntityStrategy 的 Process/RunFrames/生命周期钩子和
ActiveStrategy 的 Invoke 调用，
并能正确记录副作用（Save/Load/LevelSwitch/ControlConsole/DeferredAction）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `StrategyTestScenarioTests.cs` | EntityStrategy Harness：Process/AfterSpawn/生命周期钩子/黑板/模板克隆/副作用 |
| `ActiveStrategyTestScenarioTests.cs` | ActiveStrategy Harness：Invoke/InvokeViaEntity/数据读写/黑板/副作用/模板 |

## StrategyTestScenarioTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Process_ModifiesDataAcrossFrames` | RunFrames(5, 1.0) 后 hp 从 100 变为 50 | strategy-testing: Phase 2 |
| `RunFrame_ExecutesDeferredActions` | RunFrame 执行延迟动作 | strategy-testing: 延迟动作 |
| `Build_CallsAfterSpawn` | Build 自动触发 AfterSpawn → max_hp=200 | strategy-testing: Phase 1 |
| `SaveRequest_IsRecorded` | SaveRequests 列表记录保存请求 | strategy-testing: Phase 3 |
| `LoadRequest_IsRecorded` | LoadRequests 列表记录加载请求 | strategy-testing: Phase 3 |
| `SystemBlackboardConfig_IsAccessible` | WithSystemConfig 后策略可读取 SystemBlackboard | strategy-testing |
| `ProgressBlackboardConfig_IsAccessible` | WithProgressConfig 后策略可读取 ProgressBlackboard | strategy-testing |
| `SessionBlackboardConfig_IsAccessible` | WithSessionConfig 后策略可读取 SessionBlackboard | strategy-testing |
| `EntityName_DefaultsAndCanBeOverridden` | 默认 __test_entity__，WithEntityName("MyPlayer") 覆盖 | strategy-testing |
| `Template_CanBeRegisteredAndCloned` | WithTemplate 后策略可 Clone 获取模板数据 | strategy-testing |
| `TriggerLifecycleHooks_ExecuteStrategyHooks` | 3 个钩子触发后 hook_count=3 | strategy-testing |
| `LevelSwitchRequest_IsRecorded` | LevelSwitchRequests 列表记录关卡切换 | strategy-testing |
| `ConsoleCommand_IsRecorded` | ConsoleCommands 列表记录控制台命令 | strategy-testing |
| `MultipleFrames_AccumulateCorrectly` | 100 帧后 frame_count=100 | strategy-testing |
| `TryGetEntityData_ReturnsTrueForExistingKey` | TryGetEntityData 存在 key 返回 (true, value) | strategy-testing |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `For_EmptyStrategyIndex_ThrowsArgumentException` | 空策略索引 | ArgumentException |
| `TryGetEntityData_ReturnsFalseForMissingKey` | 不存在的 key | found=false |
| `TryGetEntityData_ReturnsFalseForTypeMismatch` | int 用 string 类型读 | found=false |
| `WithTemplate_Null_ThrowsArgumentNullException` | null 模板 | ArgumentNullException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `WithEntityName_EmptyString_UsesDefault` | "  " 空白名 | 回退到 __test_entity__ |

## ActiveStrategyTestScenarioTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Invoke_WithNoInput_ReturnsExpectedResult` | Build → Invoke 无输入返回策略默认值 42 | strategy-testing: ActiveStrategy |
| `Invoke_WithInput_PassesInputToStrategy` | Build → Invoke("hello") 传入字符串，策略返回相同值 | strategy-testing: ActiveStrategy |
| `Invoke_WithComplexInput_PassesThrough` | 匿名对象输入传入策略并原样返回 | strategy-testing: ActiveStrategy |
| `Strategy_ReadsEntityData_SetViaBuilder` | WithData 设置 counter/label，策略读取后拼入返回字符串 | strategy-testing: ActiveStrategy |
| `Strategy_WritesEntityData_HarnessCanReadBack` | 策略写入 invoke_count/invoke_status，Harness 通过 GetEntityData 读回 | strategy-testing: ActiveStrategy |
| `MultipleInvokes_IncrementData` | 3 次 Invoke 后 invoke_count=3 | strategy-testing: ActiveStrategy |
| `InvokeViaEntity_DelegatesToStrategy` | InvokeViaEntity 无输入委托到策略返回 42 | strategy-testing: ActiveStrategy |
| `InvokeViaEntity_WithInput_DelegatesCorrectly` | InvokeViaEntity("world") 委托到策略返回 "world" | strategy-testing: ActiveStrategy |
| `SystemConfig_AccessibleInStrategy` | WithSystemConfig 后策略通过 SystemBlackboard 读取 | strategy-testing: Blackboard |
| `ProgressConfig_AccessibleInStrategy` | WithProgressConfig 后策略通过 ProgressBlackboard 读取 | strategy-testing: Blackboard |
| `SessionConfig_AccessibleInStrategy` | WithSessionConfig 后策略通过 SessionBlackboard 读取 | strategy-testing: Blackboard |
| `AllThreeBlackboards_Accessible` | 三层黑板同时配置，策略全部可读取 | strategy-testing: Blackboard |
| `DefaultEntityName_IsTestEntity` | 默认实体名为 __test_entity__ | strategy-testing: ActiveStrategy |
| `CustomEntityName_PassedToStrategy` | WithEntityName("MyCustomEntity") 后策略通过 entity.Name 获取 | strategy-testing: ActiveStrategy |
| `Strategy_EnqueueBusinessDeferred_TracksCount` | Invoke → FlushDeferredActions 后 DeferredActionCount=1 | strategy-testing: 延迟动作 |
| `Strategy_MultipleDeferredActions_TracksAll` | WithData("defer_count", 3) → Invoke → Flush 后计数=3 | strategy-testing: 延迟动作 |
| `Strategy_SubmitConsoleCommand_TracksInList` | Invoke 后 ConsoleCommands 包含 "test_command arg1" | strategy-testing: 控制台 |
| `Strategy_RequestSave_TracksRequest` | Invoke 后 SaveRequests 包含 "slot_001" | strategy-testing: ActiveStrategy |
| `Strategy_RequestLoad_TracksRequest` | Invoke 后 LoadRequests 包含 "slot_002" | strategy-testing: ActiveStrategy |
| `Strategy_RequestSwitchLevel_TracksRequest` | Invoke 后 LevelSwitchRequests 包含 "dungeon" | strategy-testing: ActiveStrategy |
| `WithTemplate_RegistersTemplateForCloning` | WithTemplate 后 CloneTemplate 获取模板数据并拼入返回字符串 | strategy-testing: 模板 |
| `Entity_AfterBuild_IsAccessible` | Build 后 Entity 非 null，Name 为 __test_entity__ | strategy-testing: ActiveStrategy |
| `FoodKeyGeneration_Invoke_GeneratesSequentialKeys` | 3 次 Invoke 产生 Food_xxxx 递增 key，next_id=4 | strategy-testing: ActiveStrategy |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `GetEntityData_WithMissingKey_Throws` | 读取不存在的 key | InvalidOperationException |
| `GetEntityData_WithWrongType_Throws` | int 字段用 string 类型读 | InvalidOperationException |
| `ForActive_WithNullOrEmptyIndex_Throws` | null/空/空白 策略索引 | ArgumentException |
| `WithTemplate_WithNull_Throws` | null 模板 | ArgumentNullException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Invoke_WithNullInput_StrategyReceivesNull` | Invoke 无输入时传入 null | 策略接收 null 并返回 null |
| `Invoke_StrategyReturnsNull_IsNull` | 策略返回 null | Invoke 返回 null |
| `TryGetEntityData_WithMissingKey_ReturnsFalse` | TryGetEntityData 不存在的 key | found=false |
| `WithEntityName_EmptyOrWhitespace_ResetsToDefault` | WithEntityName("  ") 空白名 | 实体名回退到 __test_entity__ |
| `Entity_AfterBuild_StartswithCleanData` | Build 后无 WithData 时实体无数据 | TryGetEntityData 不存在 key 返回 false |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `DamageStrategy` | StrategyTestScenarioTests.cs:280 | 每帧扣血 Process，验证多帧累积效果 |
| `DeferredActionStrategy` | StrategyTestScenarioTests.cs:292 | Process 中 EnqueueBusinessDeferred |
| `AfterSpawnInitStrategy` | StrategyTestScenarioTests.cs:299 | AfterSpawn 设置 max_hp=200 |
| `SaveOnLowHpStrategy` | StrategyTestScenarioTests.cs:305 | hp≤0 时 RequestSaveGame |
| `LoadRequestStrategy` | StrategyTestScenarioTests.cs:319 | Process 中 RequestLoadGame |
| `BlackboardReaderStrategy` | StrategyTestScenarioTests.cs:325 | 读取 SystemBlackboard 到实体 data |
| `ProgressBlackboardReaderStrategy` | StrategyTestScenarioTests.cs:336 | 读取 ProgressBlackboard |
| `SessionBlackboardReaderStrategy` | StrategyTestScenarioTests.cs:347 | 读取 SessionBlackboard |
| `TemplateCloneStrategy` | StrategyTestScenarioTests.cs:358 | CloneTemplate 获取模板数据 |
| `LifecycleRecordingStrategy` | StrategyTestScenarioTests.cs:373 | 在 3 个钩子中累加 hook_count |
| `LevelSwitchStrategy` | StrategyTestScenarioTests.cs:395 | Process 中 RequestSwitchForegroundLevel |
| `ConsoleLogStrategy` | StrategyTestScenarioTests.cs:402 | Process 中 TrySubmitConsoleCommand |
| `FrameCounterStrategy` | StrategyTestScenarioTests.cs:409 | 每帧累加 frame_count |
| `NopStrategy` | StrategyTestScenarioTests.cs:419 | 空策略，用于默认值验证 |
| `SimpleAnswerStrategy` | ActiveStrategyTestScenarioTests.cs:486 | Invoke 返回 42 |
| `EchoInputStrategy` | ActiveStrategyTestScenarioTests.cs:492 | Invoke 返回 input |
| `DataWritingStrategy` | ActiveStrategyTestScenarioTests.cs:498 | Invoke 中写实体 data |
| `BusinessDeferredStrategy` | ActiveStrategyTestScenarioTests.cs:510 | EnqueueBusinessDeferred 1 或 defer_count 次 |
| `ConsoleCommandStrategy` | ActiveStrategyTestScenarioTests.cs:524 | TrySubmitConsoleCommand |
| `SaveRequestStrategy` | ActiveStrategyTestScenarioTests.cs:533 | RequestSaveGame/Load/SwitchLevel |
| `TemplateCloneStrategy` | ActiveStrategyTestScenarioTests.cs:555 | CloneTemplate 并序列化数据 |
| `DataReadingStrategy` | ActiveStrategyTestScenarioTests.cs:571 | 读取实体 data 并拼接字符串 |
| `BlackboardReadingStrategy` | ActiveStrategyTestScenarioTests.cs:590 | 从三层黑板读取数据 |
| `EntityNameStrategy` | ActiveStrategyTestScenarioTests.cs:613 | 返回 entity.Name |
| `NullReturnStrategy` | ActiveStrategyTestScenarioTests.cs:619 | Invoke 返回 null |
| `FoodKeyGeneratorStrategy` | ActiveStrategyTestScenarioTests.cs:625 | 生成 Food_xxxx 格式 key |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Harness 的 Entity 属性在未 Build 时访问的行为 | 防御性编程 | strategy-testing |
| 多策略实体测试（一个实体挂多个策略） | 策略间交互验证 | snd-entity-model |
| TriggerBeforeDead 钩子行为验证 | BeforeDead 未被测试 | strategy-testing: TriggerBeforeDead |

---

[↑ 回到 Origo.Core.Tests](README.md)
