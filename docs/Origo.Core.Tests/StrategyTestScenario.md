# 策略测试框架 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测代码: Origo.Core/Testing](../Origo.Core/Testing/README.md)
> [↔ 被测行为: usage/strategy-testing](../usage/strategy-testing.md)

## 被测行为概览

验证 StrategyTestScenario 测试框架本身的正确性。确保框架的 Harness 能正确模拟
EntityStrategy 的 Process/RunFrames/生命周期钩子和 ActiveStrategy 的 Invoke 调用，
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
