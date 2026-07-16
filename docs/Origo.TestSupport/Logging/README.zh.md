<!-- docsync-pair: Origo.TestSupport/Logging/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Logging

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

`ILogger` 的内存测试替身，按日志级别分类收集消息，用于验证日志输出内容。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TestLogger.cs` | 实现 `ILogger`，将消息按级别分桶存储到 `Debugs`、`Infos`、`Warnings`、`Errors` 四个公开列表。支持 `MinimumLevel` 过滤。 |

## 使用模式

```csharp
var logger = new TestLogger();
logger.Log(LogLevel.Error, "tag", "something broke");
Assert.Contains("something broke", logger.Errors);
```

---

[↑ 回到 TestSupport](../README.zh.md)
