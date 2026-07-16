<!-- docsync-pair: docs/Origo.SourceGeneration.Tests/Benchmarks -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# TypedData 生成产物性能基准 测试

> [↑ 回到 Origo.SourceGeneration.Tests](README.zh.md)
> [↔ 被测模块: Origo.SourceGeneration](../Origo.SourceGeneration/README.zh.md)
> [↔ 性能基线: baseline](../../benchmarks/baseline.zh.md)

## 被测行为概览

验证 `TypedData` 生成的内联存储模型相对于无优化装箱基线的性能表现。
基准引用真实 `Origo.Core` 但仅使用 `TypedData` 的 public API，
标记 `[Trait("Category","Benchmark")]` 独立于覆盖率门禁运行。

## 测试文件

| 文件 | 验证侧重点 |
|------|-----------|
| `Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | 多值类型 + `string` 的写/读/混合分发吞吐与内存分配 |

## 正确路径（性能基准）

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ValueTypes_WriteThroughput_GeneratedOperator_vs_BoxedClass` | 值类型（`int`/`long`/`float`/`double`/`bool`/`char`）写入：生成的 `explicit operator` vs 装箱类，在预算内 | Origo.SourceGeneration |
| `ValueTypes_ReadThroughput_GeneratedKind_vs_BoxedIsT` | 值类型读取：生成的 `TryGetXxx`（Kind 分发）vs 装箱 `Data is T`，命中数一致且在预算内 | Origo.SourceGeneration |
| `ReferenceType_String_GeneratedRefSlot_vs_BoxedClass` | `string` 通过 `_ref` 槽的写入与读取 vs 装箱类 | Origo.SourceGeneration |
| `StringRead_IsString_vs_BoxedIsT` | `string` 的 `IsString` 判定 vs 装箱 `Data is string` | Origo.SourceGeneration |
| `MixedDispatch_GeneratedKind_vs_BoxedIsT` | 混合类型池（int/float/bool/string/double）的 Kind 分发 vs 装箱 `is T`，命中数一致且在预算内 | Origo.SourceGeneration |

## 测试辅助设施

| 设施 | 类型 | 用途 |
|------|------|------|
| `PerfReporter` | 公共类 | 性能比对表格输出器（同时写控制台与 xUnit 测试输出），由基准用例通过 `PerfReporter.ForTest` 注入 |
| `OldTypedData` | 内部类 | 装箱基线的比较对象：以 `Type` + `object?` 存储，模拟无优化场景 |

## 基准设计决策

每个基准使用固定容量池（位掩码寻址，内存恒定）、较大的迭代次数、
一轮 warmup 加多轮计时并对两侧各取最小耗时（剔除被抢占/GC 的离群轮）。
宽松阈值（生成路径不超过基线 8× 且单基准有总时长上限）目标是守住
"不出现严重性能退化/卡死"，而非锁定绝对性能数字。

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 性能基准未覆盖适配层非系统值类型（如 Godot 类型）的读写吞吐，仅覆盖系统基元 + `string` | 适配层 `_ref` 路径性能特征未基准化 | Origo.SourceGeneration |

---

[↑ 回到 Origo.SourceGeneration.Tests](README.zh.md)
