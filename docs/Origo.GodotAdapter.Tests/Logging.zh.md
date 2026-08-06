<!-- docsync-pair: Origo.GodotAdapter.Tests/Logging -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 日志 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
> [↔ 被测模块: Origo.GodotAdapter/Logging](../Origo.GodotAdapter/Logging/README.zh.md)

## 被测行为概览

验证 GodotLogger 的委托注入模式和级别过滤：通过 `Action<LogLevel, string, string>` 委托代理日志输出、
null handler 构造时抛 ArgumentNullException、最低日志级别过滤。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotLoggerTests.cs` | GodotLogger 委托注入、null handler 拒绝（ArgumentNullException）和级别过滤 |

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Log_WithHandler_InvokesHandlerWithCorrectLevelTagAndMessage` | Log(Warning, "Tag", "msg") → handler 收到正确参数 | GodotAdapter Logging |
| `Constructor_WithNullHandler_Throws` | 构造时不传 handler | ArgumentNullException |
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

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义辅助策略；通过捕获闭包变量与本地 `Action<LogLevel, string, string>` 委托收集回调参数 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| `GodotLogger` 经真实 Godot `GD.Print`/`GD.PushWarning`/`GD.PushError` 输出的路径未覆盖（依赖 Godot 引擎运行时） | 默认无委托时的引擎级输出行为未在测试中直接验证 | Origo.GodotAdapter/Logging |
| 委托抛异常时 `GodotLogger.Log` 的传播/吞噬行为未覆盖 | 故障委托下的健壮性未验证 | Origo.GodotAdapter/Logging |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
