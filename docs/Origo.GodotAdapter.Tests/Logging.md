# 日志 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/Logging](../Origo.GodotAdapter/Logging/README.md)

## 被测行为概览

验证 GodotLogger 的委托注入模式和级别过滤：通过 `Action<LogLevel, string, string>` 委托代理日志输出、
null handler 时不抛异常、最低日志级别过滤。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotLoggerTests.cs` | GodotLogger 委托注入、null handler 安全和级别过滤 |

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Log_WithHandler_InvokesHandlerWithCorrectLevelTagAndMessage` | Log(Warning, "Tag", "msg") → handler 收到正确参数 | GodotAdapter Logging |
| `Log_WithNullHandler_DoesNotThrow` | 构造时不传 handler，Log 调用不抛异常 | GodotAdapter Logging |
| `Log_EachLogLevel_PassesCorrectLevel` | 所有四个级别均正确传递 | GodotAdapter Logging |
| `Log_NullTagAndMessage_DoesNotThrow` | null tag 和 message 不抛异常 | GodotAdapter Logging |

### 边界路径（级别过滤）

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `MinimumLevel_DefaultInfo_SuppressesDebug` | 默认 MinLevel=Info，Log(Debug) | 不触发 handler |
| `MinimumLevel_DefaultInfo_AllowsInfo` | 默认 MinLevel=Info，Log(Info) | 触发 handler |
| `MinimumLevel_ExplicitDebug_AllowsDebug` | MinLevel=Debug，Log(Debug) | 触发 handler |
| `MinimumLevel_Error_SuppressesWarning` | MinLevel=Error，Log(Warning) | 不触发 handler |
| `MinimumLevel_Error_AllowsError` | MinLevel=Error，Log(Error) | 触发 handler |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
