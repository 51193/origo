<!-- docsync-pair: Origo.TestSupport/Reporting/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Reporting

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

性能基准报告工具。提供统一的表格化输出方法和多行对比报告，供 benchmark 测试方法使用。

## 包含文件

| 文件 | 职责 |
|------|------|
| `PerfReporter.cs` | 封装 `TextWriter` 和 `ITestOutputHelper` 的性能报告器。提供 `CompareTable`（多类型对比表格）、`ReportTable`（单方法报告表格）等输出方法。 |

## 设计决策

### 为什么 PerfReporter 包含 xUnit 依赖（`ITestOutputHelper`）

Benchmark 测试需要通过 xUnit 的 `ITestOutputHelper` 输出结果才能在 CI 日志中可见。`PerfReporter` 将格式化逻辑与输出通道分离，测试代码只需通过 `PerfReporter.ForTest(output)` 构造即可获得双重输出（控制台 + test runner）。

---

[↑ 回到 TestSupport](../README.zh.md)
