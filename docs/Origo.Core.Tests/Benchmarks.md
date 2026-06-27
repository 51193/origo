# 真实模拟性能基准 (Benchmarks)

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.md) · [↔ SG 纯净微基准: Origo.SourceGeneration.Tests](../Origo.SourceGeneration.Tests/README.md)

## 被测行为概览

这套基准在贴近真实使用的路径上，对比源生成的 `TypedData`（内联存储 + Kind 分派）与无优化的「装箱进 `Dictionary<string, object>`」实现的吞吐。场景对应 SND 数据层的典型调用形态：数据写入（`SetData`）、数据读取（`TryGetData`）、跨数值类型读取（`TryGetNumeric`）、观察者通知、以及异构字典遍历。

基准标记 `[Trait("Category","Benchmark")]`，从 `ci.sh` 的全量测试运行中以 `--filter "Category!=Benchmark"` 排除，改由独立步骤 `scripts/benchmark.sh` 运行一次。该脚本同时运行本套件与 [SG 纯净微基准](../Origo.SourceGeneration.Tests/README.md)。

> **性能数值不记录在本文档。** 易变的绝对吞吐与倍率快照见权威基线 [benchmarks/baseline.md](../benchmarks/baseline.md)，本文档只描述被测能力与设计意图。

## 包含文件

| 文件 | 职责 |
|------|------|
| `Benchmarks/TypedDataRealWorldBenchmarkTests.cs` | 五个真实模拟基准：字典查找/插入、数值强转链、观察者通知、异构字典迭代；生成的 `TypedData` vs 装箱字典；固定数据集 + 大迭代 + 多轮取最小降噪，宽松断言 + 比对表格，并对每侧实测分配（`GC.GetAllocatedBytesForCurrentThread`） |
| `TestSupport/PerfReporter.cs` | 性能比对表格输出器（同时写控制台与 xUnit 测试输出） |

## 基准方法

| 基准方法 | 模拟的真实路径 | 对比内容 |
|---------|---------------|---------|
| `DictLookup_TryExtract_vs_BoxedDict` | `SndDataManager.TryGetData<T>`：字典查找命中后用 `TryGetXxx` 提取 | `Dictionary<string,TypedData>` + `TryGetXxx` vs `Dictionary<string,object>` + `is T`，覆盖 `string`/`int`/`float`/`bool`（各 2,000,000 迭代） |
| `DictInsert_FactoryCreate_vs_BoxedDict` | `SndDataManager.SetData<T>`：构造值并写入字典 | 生成工厂 `Create`/隐式转换 + 插入 vs 装箱插入，覆盖 `string`/`int`/`float`/`bool`（各 500,000 迭代） |
| `MultiTypeExtractionChain_Generated_vs_Boxed` | `TryGetNumeric` 跨数值类型逐一尝试读取 | 生成 `TryGetSingle→TryGetInt32→TryGetInt64→TryGetDouble` 链 vs `is float→is int→is long→is double` 链（int payload，2,000,000 迭代） |
| `ObserverNotify_Generated_vs_Boxed` | 观察者回调传递新旧值并判型 | 传递 `(TypedData, TypedData)` + `TryGetString` vs 传递 `(object?, object?)` + `is string`（2,000,000 迭代） |
| `HeterogeneousDictIteration_GeneratedData_vs_BoxedDict` | 遍历异构数据字典，逐项读取 `.Data` | 生成 `.Data`（经 `TypedDataObjectConverter.ToObject`）vs 纯 `object` 直通（2,000 轮 × 1024 项 = 2,048,000 次读取） |

每个基准的数据集混合 `int`/`float`/`bool`/`string`/`double` 五种类型（`i % 5` 轮换），以反映异构 SND 数据的真实分布。

## 测试辅助设施

`PerfReporter`（见 [Origo.Core.Tests README — 测试辅助设施](README.md)）按统一表格格式打印「方法 / 迭代数 / 耗时 / 吞吐 / 分配」，并双通道输出到 `Console.Out` 与 `ITestOutputHelper`，确保 CI 与本地均可见结果。

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 不覆盖 GodotAdapter 注册类型（`Vector2`/`Vector3` 等）的真实场景吞吐 | 适配层多层分派性能不在本套件验证 | 由 [Origo.GodotAdapter.Tests/Serialization](../Origo.GodotAdapter.Tests/Serialization.md) 的 `GodotTypedDataPerformanceTests` 单独覆盖 |
| 不覆盖并发/多线程读写 | 多线程下的争用与可见性未测 | 框架采用单线程帧模型（见 [手册根 README — 设计原则](../README.md)） |
| 不覆盖按 Kind 直接分派的异构迭代替代路径 | 仅测 `.Data`（`ToObject`）这一条迭代路径 | [Snd/Metadata](../Origo.Core/Snd/Metadata/README.md) 的 Kind 分派 |
| 绝对吞吐/倍率/分配均不作断言（仅打印 + 单基准时间上限） | 性能退化无法自动捕获，需人工对照基线 | [benchmarks/baseline.md](../benchmarks/baseline.md) |

## 设计决策

### 为什么基准宽松：只设时间上限，不断言「更快」

本套件对每个基准仅断言单基准总耗时低于 8 秒上限（`AssertInCap`），**不**设固定倍率上限，也不要求生成路径快于装箱基线。真实路径叠加了字典查找、对象转换等成本，生成路径在部分读取场景本就允许慢于装箱基线（详见基线文件）。基准的目的是「守住不卡死、可长期跟踪相对趋势」，而非锁定绝对数字或强制不退化——后者会因机器与运行时差异频繁误报。

> 这与 [SG 纯净微基准](../Origo.SourceGeneration.Tests/README.md) 略有不同：SG 套件额外断言生成路径不超过基线 8×；本套件因真实路径基线更复杂，只保留时间上限。

### 为什么标记 `Category=Benchmark` 并独立运行

基准迭代次数大、耗时长，若与受覆盖率门禁约束的测试一起跑会拖慢常规 CI，且不应被统计进行覆盖率。标记 `[Trait("Category","Benchmark")]` 后由 `ci.sh` 以 `--filter "Category!=Benchmark"` 排除，改由 `scripts/benchmark.sh` 在独立步骤运行一次，既打印比对表格又执行宽松断言，避免被运行两次。

### 为什么用固定数据集 + 多轮取最小降噪

为抵抗 OS 时间片轮转与 GC 带来的测量噪声，每个基准使用固定容量的字典/数据集（内存恒定）、较大的迭代次数（使单轮耗时跨多个时间片），并以 1 轮 warmup 加多轮计时、对生成与装箱两侧各取最小耗时，剔除被抢占/GC 的离群轮。

### 为什么分配测量放在独立 NoInlining 方法里

每个基准在计时轮之外，对生成/装箱两侧各跑一次专用测量轮，取 `GC.GetAllocatedBytesForCurrentThread()` 前后差值作为该轮分配。测量循环被抽到独立的 `[MethodImpl(NoInlining)]` 方法中：若内联进计时方法体（或用捕获局部变量的 lambda），会改变计时循环的代码生成（闭包字段间接、循环对齐），从而污染吞吐数字。放在独立方法里使计时循环保留未插桩版的代码生成，分配与时间互不干扰。

### 为什么只用 public API

基准仅使用 `TypedData` 的 public API（显式转换运算符、`TryGetXxx`、`TryGetString`、`FromObject`、`.Data`）与 `System.Collections.Generic.Dictionary`，无需向测试项目开放 Core 内部成员，使基准与真实下游消费者的调用形态一致。

---

> [↑ 回到 Origo.Core.Tests](README.md)
