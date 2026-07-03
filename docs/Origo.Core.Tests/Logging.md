# 日志 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Logging](../Origo.Core/Logging/README.md)

## 被测行为概览

验证 LogMessageBuilder 的结构化构建：纯消息、带时间戳（SetElapsedMs）、
带上下文（AddContext）、null/空白键跳过、组合使用、零值时间戳。

验证 `Logger<T>` 泛型包装器的行为：根据类型名自动生成 tag、
显式接口实现兼容手动 tag、null inner 守卫、与 NullLogger 组合。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `LogMessageBuilderTests.cs` | LogMessageBuilder 结构化构建 |
| `LoggerGenericTests.cs` | `Logger<T>` 类型感知日志包装器 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Build_PlainMessage` | Build("hello") 返回 "hello" | Logging |
| `SetElapsedMs_IncludesTimestamp` | SetElapsedMs → "[+Xms]" 前缀 | Logging |
| `AddContext_AppendsContext` | AddContext("key","val") → "test \| key=val" | Logging |
| `AddContext_MultipleEntries_AllIncluded` | 多个 AddContext → 逗号分隔 | Logging |
| `Combined_ElapsedAndContext` | timestamp + context 组合正确 | Logging |
| `SetElapsedMs_Zero_NotTruncated` | 0ms → "[+0.00ms]" | Logging |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `AddContext_NullKey_Skipped` | null key | 跳过 |
| `AddContext_NullValue_Skipped` | null value | 跳过 |
| `AddContext_WhitespaceKey_Skipped` | 空白 key | 跳过 |

## 已知覆盖缺口

无——此模块仅一个简单工具类，测试覆盖已完整。

## LoggerGenericTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 |
|---------|-----------|
| `LoggerT_TagDerived_FromTypeName` | `Logger<TestCategory>.Log()` 自动以 `typeof(T).Name` 作为 tag |
| `LoggerT_ExplicitInterface_UsesProvidedTag` | 通过 `ILogger` 基接口调用时使用显式传入的 tag |
| `LoggerT_DifferentTypes_HaveDifferentTags` | `Logger<string>` 和 `Logger<int>` 分别使用 `"String"` 和 `"Int32"` |
| `LoggerT_WrapsNullLogger` | `new Logger<T>(NullLogger.Instance)` 不抛异常 |
| `LoggerT_GenericType_TagUsesFriendlyName` | `Logger<GenericTest<int,string>>` 使用 CLR 友好名称 |

### 错误路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `LoggerT_Constructor_NullInner_Throws` | `new Logger<T>(null)` | `ArgumentNullException` |

---

[↑ 回到 Origo.Core.Tests](README.md)
