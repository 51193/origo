# 控制台系统 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Runtime/Console](../Origo.Core/Runtime/Console/README.md)
> [↔ 被测行为: usage/console-commands](../usage/console-commands.md)

## 被测行为概览

验证控制台命令系统的全链路：命令解析（位置参数/命名参数/混合模式）、命令路由（注册/分发/未找到）、
输入队列（轮询式出队）、输出通道（发布-订阅）、11 个 Core 内置命令、2 个适配层命令处理、类型推断（bb_set/entity_set_data）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `ConsoleCommandParserTests.cs` | 命令解析：空行/空白行/单命令/位置参数/命名参数/无效命名参数 |
| `ConsoleCommandRouterTests.cs` | 命令路由：注册/分发/未注册命令/help 命令 |
| `ConsoleInputBufferTests.cs` | 输入队列：Enqueue/Dequeue/FIFO/空队列 |
| `ConsoleOutputChannelTests.cs` | 输出通道：Subscribe/Publish/Unsubscribe/多订阅者 |
| `ConsoleTests.cs` | 集成测试：spawn 命令 positional/named 模式、模板不存在、重名 |
| `ConsoleCommandExtendedTests.cs` | 命令扩展/边缘测试 |
| `ConsoleTypeInferenceTests.cs` | 类型推断：bb_set Int32/Single/Boolean/String、entity_set_data 新键推断+已有键类型保留 |
| `OrigoConsoleLoggingTests.cs` | 控制台日志记录：日志级别正确性、消息顺序、Tag 一致性、内容完整性（行为验证，不耦合格式字符串） |
| `EntityDataCommandHandlerTests.cs` | entity_get_data / entity_set_data 命令 |
| `InvokeStrategyCommandHandlerTests.cs` | invoke_strategy 命令 |
| `SndCountCommandHandlerTests.cs` | snd_count 命令 |
| `SpawnTemplateCommandHandlerTests.cs` | spawn 命令错误路径：混合参数格式、缺少 name 参数 |

## ConsoleCommandParserTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `TryParse_SingleCommand` | "help" 解析为命令名 + 空参数 | console-commands |
| `TryParse_PositionalArgs` | "spawn myName myTemplate" 解析出 2 个位置参数 | console-commands |
| `TryParse_NamedArgs` | "spawn name=myName template=myTpl" 解析出 2 个命名参数 | console-commands |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `TryParse_EmptyLine_Fails` | 空行 | 返回 false + error |
| `TryParse_WhitespaceLine_Fails` | 空白行 | 返回 false + error |
| `TryParse_InvalidNamedArg_Fails` | "cmd =value"（无 key） | 返回 false + error |
| `TryParse_NamedArgMissingValue_Fails` | "cmd key="（无 value） | 返回 false + error |

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

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `BlackboardSet_UnknownLayer_ReturnsError` | bb_set unknown key 42 | 返回 false + error 含 "layer" |
| `EntitySetData_EntityNotFound_ReturnsError` | entity_set_data nonexistent hp 50 | 返回 false + error 含 "not found" |
| `RequestSaveGame_ThrowsOnNullId` | null saveId | ArgumentException |
| `RequestLoadGame_ThrowsOnNullId` | null loadId | ArgumentException |

## ConsoleTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `OrigoConsole_SpawnTemplate_Positional_SpawnsWithResolvedName` | spawn 命令通过位置参数创建实体 | console-commands: spawn |
| `OrigoConsole_SpawnTemplate_Named_SpawnsWithNameAndTemplate` | spawn 命令通过命名参数创建实体 | console-commands: spawn |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `OrigoConsole_SpawnTemplate_MissingTemplate_WritesError` | spawn 不存在的模板 | 输出错误消息 |
| `OrigoConsole_SpawnTemplate_DuplicateName_WritesErrorAndSkipsSecondSpawn` | spawn 重名实体 | 第一个成功，第二个报错 |

## SpawnTemplateCommandHandlerTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `MixNamedAndPositional_ReturnsError` | 位置参数和命名参数混用 | 返回 false + error 含 "mix" |
| `NamedMissingName_ReturnsError` | 命名参数缺 name | 返回 false + error 含 "name" |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| ConsoleCommandRouter 移除已注册 Handler 后的行为 | 动态卸载命令 | — |
| 并发 TryDequeueCommand 和 Enqueue 的线程安全 | 多线程输入 | ConsoleInputBuffer |
| TCP 远程控制台断开重连后的输出缓冲 | 重连时历史输出是否推送 | Origo.ConsoleBridge |

---

[↑ 回到 Origo.Core.Tests](README.md)
