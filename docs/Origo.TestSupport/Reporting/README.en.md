<!-- docsync-pair: Origo.TestSupport/Reporting/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->

# Reporting

> [↑ Back to TestSupport](../README.en.md)

## Overview

Performance benchmark reporting utilities. Provides unified tabular output methods and multi-row comparison reports for benchmark test methods.

## File Inventory

| File | Responsibility |
|------|---------------|
| `PerfReporter.cs` | Performance reporter wrapping `TextWriter` and `ITestOutputHelper`. Provides `CompareTable` (multi-type comparison table), `ReportTable` (single-method report table), and `newline()` separator methods; duplicate `BENCH` metric keys within one test process throw `InvalidOperationException` |

## Design Decisions

### Why PerfReporter takes an xUnit dependency (`ITestOutputHelper`)

Benchmark tests must output results through xUnit's `ITestOutputHelper` for CI log visibility. `PerfReporter` separates formatting logic from the output channel; test code simply constructs via `PerfReporter.ForTest(output)` to get dual output (console + test runner).

---

[↑ Back to TestSupport](../README.en.md)
