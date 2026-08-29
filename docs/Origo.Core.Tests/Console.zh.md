<!-- docsync-pair: Origo.Core.Tests/Console -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 控制台系统 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Runtime/Console](../Origo.Core/Runtime/Console/README.zh.md)
> [↔ 被测行为: usage/console-commands](../usage/console-commands.zh.md)

## 被测行为概览

验证控制台命令系统的全链路：命令解析（位置参数/命名参数/混合模式）、命令路由（注册/分发/未找到/大小写不敏感/重复注册拒绝）、
输入队列（轮询式出队、FIFO、裁剪、清空）、`IConsoleInputSource` 接口契约、输出通道（发布-订阅、异常传播、null 拒绝）、
14 个内置命令处理（11 Core + 3 GodotAdapter）、类型推断（bb_set/entity_set_data）、控制台日志记录（级别/顺序/Tag/内容完整性）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `ConsoleCommandParserTests.cs` | 命令解析：空行/空白行/单命令/位置参数/命名参数/无效命名参数 |
| `ConsoleCommandRouterTests.cs` | 命令路由：注册/分发/未注册命令/大小写不敏感/重复注册拒绝/null handler |
| `ConsoleInputBufferTests.cs` | 输入队列：Enqueue/Dequeue/FIFO/裁剪/空白忽略/清空 |
| `ConsoleOutputChannelTests.cs` | 输出通道：Subscribe/Publish/Unsubscribe/多订阅者/null 广播/异常传播 |
| `ConsoleCommandExtendedTests.cs` | 内置命令端到端：help/find_entity/kill_all/bb_*、晚注册、参数校验、spawn |
| `ConsoleTypeInferenceTests.cs` | 类型推断：bb_set Int32/Single/Boolean/String、entity_set_data 新键推断+已有键类型保留 |
| `OrigoConsoleLoggingTests.cs` | 控制台日志记录：日志级别正确性、消息顺序、Tag 一致性、内容完整性（行为验证，不耦合格式字符串） |
| `ConsoleInputSourceContractTests.cs` | IConsoleInputSource 接口契约：往返/FIFO/裁剪/空白忽略/清空/null 忽略 |
| `EntityDataCommandHandlerTests.cs` | entity_get_data / entity_set_data 命令 |
| `InvokeStrategyCommandHandlerTests.cs` | invoke_strategy 命令 |
| `SndCountCommandHandlerTests.cs` | snd_count 命令 |
| `SpawnTemplateCommandHandlerTests.cs` | spawn 命令错误路径：混合参数格式、缺少 name 参数 |

## ConsoleCommandParserTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ConsoleCommandParser_TryParse_SingleCommand` | "help" 解析为命令名 + 空参数 | console-commands |
| `ConsoleCommandParser_TryParse_PositionalArgs` | "spawn myName myTemplate" 解析出 2 个位置参数 | console-commands |
| `ConsoleCommandParser_TryParse_NamedArgs` | "spawn name=myName template=myTpl" 解析出 2 个命名参数 | console-commands |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ConsoleCommandParser_TryParse_EmptyLine_Fails` | 空行 | 返回 false + error |
| `ConsoleCommandParser_TryParse_WhitespaceLine_Fails` | 空白行 | 返回 false + error |
| `ConsoleCommandParser_TryParse_InvalidNamedArg_Fails` | "cmd =value"（无 key） | 返回 false + error |
| `ConsoleCommandParser_TryParse_NamedArgMissingValue_Fails` | "cmd key="（无 value） | 返回 false + error |
| `ConsoleCommandParser_TryParse_DuplicateNamedArg_Fails` | "spawn name=a name=b"（重复命名参数） | 返回 false + error（含参数名），不静默覆盖 |

## ConsoleCommandRouterTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ConsoleCommandRouter_Register_And_TryExecute_Success` | 注册 handler 后分发执行成功 | console-commands |
| `ConsoleCommandRouter_Register_CaseInsensitive` | 命令名大小写不敏感（"TEST" 匹配 "Test"） | console-commands |
| `ConsoleCommandRouter_Register_DuplicateName_Throws` | 重复注册同名命令 | InvalidOperationException（命令名必须唯一） |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ConsoleCommandRouter_TryExecute_UnknownCommand_ReturnsFalse` | 未注册命令 | 返回 false + error 含 "Unknown command" |
| `ConsoleCommandRouter_Register_NullHandler_Throws` | Register(null) | ArgumentNullException |

## ConsoleInputBufferTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ConsoleInputBuffer_Enqueue_And_TryDequeue` | Enqueue 后 TryDequeueCommand 取回原值 | console-commands |
| `ConsoleInputBuffer_Enqueue_TrimsInput` | "  hello  " 入队后裁剪为 "hello" | console-commands |
| `ConsoleInputBuffer_FIFO_Order` | 多条命令按先进先出顺序出队 | console-commands |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `ConsoleInputBuffer_TryDequeue_EmptyQueue_ReturnsFalse` | 空队列出队 | 返回 false，line 为 null |
| `ConsoleInputBuffer_Enqueue_WhitespaceIgnored` | 入队空白/空字符串 | 不入队，出队返回 false |
| `ConsoleInputBuffer_Clear_EmptiesQueue` | Clear 后出队 | 返回 false |

## ConsoleOutputChannelTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ConsoleOutputChannel_Subscribe_And_Publish` | 订阅后 Publish 收到消息 | console-commands |
| `ConsoleOutputChannel_Unsubscribe_StopsReceiving` | Unsubscribe 后不再收到后续消息 | console-commands |
| `ConsoleOutputChannel_MultipleSubscribers` | 多订阅者同时收到广播 | console-commands |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ConsoleOutputChannel_Subscribe_ThrowsOnNull` | Subscribe(null) | ArgumentNullException |
| `ConsoleOutputChannel_Publish_FirstListenerThrows_SecondStillReceives` | 首个订阅者抛异常 | 异常传播，但后续订阅者仍收到消息 |
| `ConsoleOutputChannel_Publish_FirstListenerThrows_ExceptionPropagates` | 多订阅者抛异常 | 传播首个异常（"e1"） |
| `ConsoleOutputChannel_Publish_MultipleListenerFailures_AggregatesEveryFailure` | 三个订阅者全部抛异常 | AggregateException 包含全部 3 个 inner exception |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `ConsoleOutputChannel_Unsubscribe_InvalidId_ReturnsFalse` | Unsubscribe 不存在的 id | 返回 false |
| `ConsoleOutputChannel_Publish_Null_Throws` | Publish(null) | ArgumentNullException |

## ConsoleCommandExtendedTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `HelpCommand_ListsAllRegisteredCommands` | help 列出全部注册命令（含 spawn/snd_count） | console-commands: help |
| `ClearEntitiesCommand_ClearsAll` | kill_all 命令输出本帧标记数量 | console-commands: kill_all |
| `BlackboardSetGet_RoundTrip` | bb_set 后 bb_get 取回 int 值 | console-commands: bb_set/bb_get |
| `BlackboardSetGet_StringValue` | bb_set/bb_get 字符串值往返 | console-commands |
| `BlackboardSetGet_BoolValue` | bb_set/bb_get bool 值往返，类型为 Boolean | console-commands |
| `BlackboardKeys_ListsKeys` | bb_keys 列出已设置的键 | console-commands: bb_keys |
| `RegisterHandler_LateRegistration_CommandAvailable` | 运行期晚注册的 handler 命令立即可用并出现在 help | console-commands |
| `GetRegisteredNames_ReturnsSortedNames` | GetRegisteredNames 返回含已注册命令名 | console-commands |
| `ConsoleCommandHandlerBase_ExactArgs_Succeeds` | 参数数量正好等于要求时执行成功 | console-commands |
| `HelpCommand_ShowsHelpTextForEachCommand` | help 显示各命令的 HelpText | console-commands: help |
| `SpawnCommand_NamedArgs_SpawnsEntity` | spawn name=.. template=.. 成功生成实体 | console-commands: spawn |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `FindEntityCommand_NotFound_ReportsNotFound` | find_entity 查询不存在实体 | 输出含 "not found" |
| `FindEntityCommand_MissingArg_ReportsUsage` | find_entity 缺参数 | 输出含 "Invalid argument count." |
| `BlackboardGet_MissingKey_ReportsNotFound` | bb_get 查询不存在键 | 输出含 "not found" |
| `BlackboardSet_InvalidLayer_ReportsError` | bb_set 非法层名 | 输出含 "Unknown" |
| `BlackboardGet_MissingArgs_ReportsUsage` | bb_get 缺参数 | 输出含 "Invalid argument count." |
| `BlackboardSet_MissingArgs_ReportsUsage` | bb_set 缺参数 | 输出含 "Invalid argument count." |
| `BlackboardKeys_MissingArgs_ReportsUsage` | bb_keys 缺参数 | 输出含 "Invalid argument count." |
| `ConsoleCommandHandlerBase_TooFewArgs_ReturnsErrorWithHelpText` | 位置参数少于 Min | 返回 false + error 含 "Invalid argument count." 与 HelpText |
| `ConsoleCommandHandlerBase_TooManyArgs_ReturnsErrorWithHelpText` | 位置参数多于 Max | 返回 false + error 含 "Invalid argument count." |
| `SpawnCommand_NamedMissingTemplate_ReportsError` | spawn 命名参数缺 template | 输出含 "template" |
| `SpawnCommand_PositionalWrongCount_ReportsUsage` | spawn 位置参数数量错误 | 输出含 "Usage" |
| `SpawnCommand_PositionalSingleArg_ReportsUsage` | spawn 仅单个位置参数 | 输出含 "Usage" |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `BlackboardKeys_EmptyBlackboard` | 空黑板 bb_keys | 输出含 "empty" |
| `ConsoleCommandHandlerBase_UnlimitedMax_AcceptsAnyCount` | MaxPositionalArgs = -1 | 接受任意数量位置参数并成功 |

## ConsoleTypeInferenceTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `BlackboardSet_IntLiteral_StoredAsInt32` | bb_set system score 42 → TryGet<int> 返回 (true, 42) | console-commands: bb_set |
| `BlackboardSet_NegativeInt_StoredAsInt32` | bb_set system neg -5 → Int32(-5) | console-commands |
| `BlackboardSet_FloatLiteral_StoredAsSingle` | bb_set system pi 3.14 → Single(3.14) | console-commands |
| `BlackboardSet_TrueLiteral_StoredAsBoolean` | bb_set system flag true → Boolean(true) | console-commands |
| `BlackboardSet_FalseLiteral_StoredAsBoolean` | bb_set system flag2 false → Boolean(false) | console-commands |
| `BlackboardSet_NonNumericLiteral_StoredAsString` | bb_set system msg hello_world → String | console-commands |
| `EntitySetData_NewKey_IntLiteral_StoredAsInt32` | entity_set_data player hp 100 → Int32 | console-commands: entity_set_data |
| `EntitySetData_NewKey_FloatLiteral_StoredAsSingle` | entity_set_data player speed 1.5 → Single | console-commands |
| `EntitySetData_NewKey_BoolLiteral_StoredAsBoolean` | entity_set_data player alive true → Boolean | console-commands |
| `EntitySetData_NewKey_StringLiteral_StoredAsString` | entity_set_data player tag hero → String | console-commands |
| `EntitySetData_ExistingKey_PreservesType` | 已有 float 类型的 hunger 键，写 15 → 保持 Single(15.0f) | console-commands: entity_set_data |
| `BlackboardSet_BeyondIntRange_StoredAsInt64` | bb_set system big 3000000000（超出 int 范围）→ Int64(3000000000) | console-commands: bb_set |
| `EntitySetData_NewKey_BeyondIntRange_StoredAsInt64` | entity_set_data player coins 3000000000 → Int64 | console-commands: entity_set_data |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `BlackboardSet_UnknownLayer_ReturnsError` | bb_set unknown key 42 | 返回 false + error 含 "layer" |
| `EntitySetData_EntityNotFound_ReturnsError` | entity_set_data nonexistent hp 50 | 返回 false + error 含 "not found" |
| `EntitySetData_ExistingKeyUnparseableValue_ReturnsErrorAndKeepsValue` | 已有 int 键写不可解析值 not_a_number | 返回 false + error 含 "Cannot parse"，原值保留 |

## OrigoConsoleLoggingTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ProcessPending_SimpleCommand_LogsThreeDebugMessagesAndNoWarnings` | 单命令产生 3 条 Debug 日志且无 Warning | console-commands |
| `ProcessPending_MultipleCommands_LogsThreePerCommand` | 每条命令产生 3 条 Debug，按命令名分布正确 | console-commands |
| `ProcessPending_UnknownCommand_LogsFailureAtDebugLevel` | 未知命令失败仅记 Debug，无 Warning | console-commands |
| `ProcessPending_HandlerReturnsError_LogsFailureAtDebugLevel` | handler 返回错误仅记 Debug | console-commands |
| `ProcessPending_MixedSuccessAndFailure_LogLevelsCorrect` | 成功与失败混合时日志级别均为 Debug | console-commands |
| `ProcessPending_ReceiveBeforeExecuteBeforeResult_OrderCorrect` | 接收→执行→结果日志按顺序出现 | console-commands |
| `ProcessPending_HandlerReturnsErrorWithNullMessage_LogsFailureAtDebugLevel` | handler 返回 null 错误消息仍记 Debug | console-commands |
| `ProcessPending_ParseError_LoggedAtDebugLevel` | 解析错误记 Debug 含 "Parse error" | console-commands |
| `ProcessPending_AllDebugMessages_HaveCorrectTag` | 全部 Debug 消息以 "OrigoConsole: " 开头 | console-commands |
| `ProcessPending_NormalOperation_ProducesNoWarnings` | 正常操作无 Warning | console-commands |
| `ProcessPending_PositionalArgs_AppearInLog` | 位置参数出现在日志中 | console-commands |
| `ProcessPending_NamedArgs_AppearInLog` | 命名参数出现在日志中 | console-commands |
| `ProcessPending_SuccessCommand_IncludesElapsedTime` | 成功命令日志含耗时 "ms" | console-commands |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ProcessPending_HandlerThrowsException_ThrowsUnhandledToCaller` | handler 抛出异常 | 异常向上传播给调用方（InvalidOperationException） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `ProcessPending_EmptyQueue_ProducesNoLogMessages` | 空队列 ProcessPending | 无任何日志 |
| `ProcessPending_TrimmedCommand_StillProcessed` | 含前后空白的命令 | 裁剪后正常处理 |
| `ProcessPending_EmptyAfterTrim_Skipped` | 裁剪后为空 | 跳过，无日志 |
| `ProcessPending_LongCommandLine_FullContentLogged` | 500 字符超长命令 | 完整内容记入日志 |
| `ProcessPending_UnicodeCommand_CharactersPreserved` | 含 Unicode 字符的命令 | 字符完整保留 |
| `ProcessPending_CommandWithEmbeddedQuotes_LoggedCorrectly` | 含内嵌引号的命令 | 正确记录 |

## ConsoleInputSourceContractTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Enqueue_And_TryDequeue_RoundTrip` | Enqueue 后 TryDequeueCommand 取回原值 | console-commands |
| `Enqueue_FifoOrder_Preserved` | 多条命令按 FIFO 出队 | console-commands |
| `Enqueue_TrimsWhitespaceAroundContent` | 入队裁剪首尾空白保留内部内容 | console-commands |
| `Enqueue_AfterClear_WorksNormally` | Clear 后再 Enqueue 正常工作 | console-commands |
| `Clear_EmptiesAllPendingCommands` | Clear 清空全部待处理命令 | console-commands |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TryDequeue_EmptyQueue_ReturnsFalse` | 空队列出队 | 返回 false，cmd 为 null |
| `TryDequeue_AfterExhausting_ReturnsFalse` | 取尽后再出队 | 返回 false |
| `Enqueue_EmptyString_Ignored` | 入队空字符串 | 忽略 |
| `Enqueue_WhitespaceOnly_Ignored` | 入队纯空白 | 忽略 |
| `Clear_OnAlreadyEmpty_DoesNotThrow` | 对空队列 Clear | 不抛异常 |
| `Enqueue_Null_Ignored` | 入队 null | 忽略 |

## EntityDataCommandHandlerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `EntitySetData_IntValue_StoresCorrectly` | entity_set_data 写 int 并输出确认 | console-commands: entity_set_data |
| `EntitySetData_FloatValue_StoresCorrectly` | entity_set_data 写 float | console-commands |
| `EntitySetData_BoolValue_StoresCorrectly` | entity_set_data 写 bool | console-commands |
| `EntitySetData_StringValue_StoresCorrectly` | entity_set_data 写 string | console-commands |
| `EntitySetData_PreservesExistingIntType` | 已有 int 键再写保持 int | console-commands |
| `EntitySetData_PreservesExistingFloatType` | 已有 float 键再写保持 float | console-commands |
| `EntitySetData_PreservesExistingBoolType` | 已有 bool 键再写保持 bool | console-commands |
| `EntitySetData_PreservesExistingStringType` | 已有 string 键再写保持 string | console-commands |
| `EntityGetData_Found_ReportsValueAndType` | entity_get_data 输出值与类型（Int32） | console-commands: entity_get_data |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `EntitySetData_EntityNotFound_ReportsError` | entity_set_data 实体不存在 | 输出含 "not found" |
| `EntityGetData_EntityNotFound_ReportsError` | entity_get_data 实体不存在 | 输出含 "not found" |
| `EntityGetData_NotFound_ReportsNotFound` | entity_get_data 键不存在 | 输出含 "not found on entity" |
| `EntityGetData_MissingArgs_ReportsUsage` | entity_get_data 缺参数 | 输出含 "Invalid argument count." |
| `EntitySetData_MissingArgs_ReportsUsage` | entity_set_data 缺参数 | 输出含 "Invalid argument count." |

## InvokeStrategyCommandHandlerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `InvokeStrategy_NoInput_ReturnsResult` | invoke_strategy 无输入调用主动策略并输出结果 | console-commands: invoke_strategy |
| `InvokeStrategy_WithInput_PassesToStrategy` | invoke_strategy 将 JSON 输入传给策略 | console-commands: invoke_strategy |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `InvokeStrategy_MissingEntity_OutputsError` | invoke_strategy 目标实体不存在 | 返回 false + error 含实体名 |
| `InvokeStrategy_NotActiveStrategy_OutputsError` | 调用未注册的主动策略索引 | 返回 false + error 含策略索引 |

## SndCountCommandHandlerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndCount_PublishesEntityCount` | snd_count 输出当前实体数量 "Snd count: 2" | console-commands: snd_count |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SndCount_WithNoEntities_PublishesZero` | 无实体（无前台会话） | 输出 "Snd count: 0" |

## SpawnTemplateCommandHandlerTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SpawnTemplateCommandHandler_MixNamedAndPositional_ReturnsError` | 位置参数和命名参数混用 | 返回 false + error 含 "mix" |
| `SpawnTemplateCommandHandler_NamedMissingName_ReturnsError` | 命名参数缺 name | 返回 false + error 含 "name" |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `StubHandler` | ConsoleCommandRouterTests.cs | IConsoleCommandHandler 桩，记录是否被执行，验证路由分发 |
| `TestPingHandler` | ConsoleCommandExtendedTests.cs | IConsoleCommandHandler 桩，"ping"→发布 "pong"，验证晚注册 |
| `TestMinMaxHandler` | ConsoleCommandExtendedTests.cs | ConsoleCommandHandlerBase 桩，可配置 Min/Max 参数数量，验证参数校验 |
| `FailingHandler` | OrigoConsoleLoggingTests.cs | IConsoleCommandHandler 桩，始终返回失败，验证失败日志 |
| `NullErrorHandler` | OrigoConsoleLoggingTests.cs | IConsoleCommandHandler 桩，返回 false 且错误消息为 null，验证空错误日志 |
| `ThrowingHandler` | OrigoConsoleLoggingTests.cs | IConsoleCommandHandler 桩，TryExecute 抛异常，验证异常传播 |
| `QueryNameStrategy` | InvokeStrategyCommandHandlerTests.cs | ActiveStrategyBase 桩，Invoke 返回实体名，验证 invoke_strategy 无输入 |
| `CmdWithInputStrategy` | InvokeStrategyCommandHandlerTests.cs | ActiveStrategyBase 桩，Invoke 回显输入，验证 invoke_strategy 带输入 |
| `CollectingConsoleOutputChannel` | InvokeStrategyCommandHandlerTests.cs | IConsoleOutputChannel 桩，收集输出行供断言 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| ConsoleCommandRouter 移除已注册 Handler 后的行为 | 动态卸载命令 | — |
| 并发 TryDequeueCommand 和 Enqueue 的线程安全 | 多线程输入 | ConsoleInputBuffer |
| TCP 远程控制台断开重连后的输出缓冲 | 重连时历史输出是否推送 | Origo.ConsoleBridge |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
