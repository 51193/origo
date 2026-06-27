# Origo.SourceGeneration.Tests

> [↑ 回到 Origo.manual](../README.md) · [↔ 被测模块: Origo.SourceGeneration](../Origo.SourceGeneration/README.md)

## 概述

`Origo.SourceGeneration.Tests` 包含两类测试：

- **生成器行为测试**：直接驱动 `TypedDataGenerator`，在内存编译上运行生成器并断言生成的源、生成器诊断以及"原始源 + 生成源"合并编译的结果。它把生成器作为普通库引用（非分析器附加），以便在测试中实例化并运行。
- **生成产物性能基准**：通过引用 `Origo.Core` 取得生成的 `TypedData`（仅用 public API），跨多种值类型与引用类型 `string` 对比内联存储/Kind 分发与无优化装箱实现的吞吐。标记 `[Trait("Category","Benchmark")]`，在独立的 CI 步骤（`scripts/benchmark.sh`）中运行一次并打印比对表格，与受覆盖率门禁约束的测试运行分离。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GeneratorTestHarness.cs` | 构造内存 `CSharpCompilation`，运行 `TypedDataGenerator`，暴露生成源、生成器诊断、合并编译错误 |
| `TypedDataGeneratorTests.cs` | 生成器行为测试：Home/Adapter 模式输出、两存储模型、`ORIGOSG001`/`ORIGOSG002` 诊断、生成确定性 |
| `Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | 生成产物性能基准：多值类型 + `string` 的写/读/混合分发，生成的内联 `TypedData` vs 无优化装箱；固定池 + 大迭代 + 多轮取最小降噪，宽松阈值 + 比对表格，并对每侧实测分配（`GC.GetAllocatedBytesForCurrentThread`，置于独立 `NoInlining` 方法以免污染计时） |
| `TestSupport/PerfReporter.cs` | 性能比对表格输出器（同时写控制台与 xUnit 测试输出） |

## 测试能力

| 能力 | 验证内容 |
|------|---------|
| Home 模式生成 | 系统基础类型生成 `KindMap`、`AsXxx`/`TryGetXxx`、`explicit operator`、`TypedDataFactory<T>`、`ModuleInitializer` 注册，且合并编译零错误 |
| 两存储模型 | `string` 与适配层非系统值类型走 `_ref`；Home 不产出任何内联桩辅助方法（回归断言：不含 `BitsFrom`/`ReadBitsAs`/`Pack`/`return default;`） |
| Adapter 模式生成 | 非系统值类型与引用类型统一走 `_ref`，生成扩展方法、Kind/Converter/TypeMap 三个 `ModuleInitializer`，且合并编译零错误（经 `InternalsVisibleTo` 访问 `TypedData` 内部成员） |
| 诊断 `ORIGOSG001` | 系统基础类型注册到适配层组时报告为 Error，并从生成中剔除 |
| 诊断 `ORIGOSG002` | 宿主程序集注册不可内联值类型时报告为 Error，合法类型仍正常生成 |
| 空输入 | 无 `SndInlineTypes` 特性时不产出任何源 |
| Kind 编号 | `StartKind` 偏移生效且按声明顺序递增 |
| 生成确定性 | 相同输入两次运行产出完全一致的源文本 |
| 生成产物性能基准 | 生成的内联 `TypedData` 对比无优化装箱，覆盖值类型（`int`/`long`/`float`/`double`/`bool`/`char`）+ 引用类型 `string` 的写/读，以及混合分发；用固定池 + 位掩码寻址 + 大迭代 + warmup + 多轮取最小耗时抗调度噪声，断言生成路径不超过基线 8× 且单基准有总时长上限，并打印比对表格 |

## 行覆盖率门禁

由 Coverlet 强制 `Origo.SourceGeneration` 行覆盖率 ≥ 85%（在 CI 与本地 `dotnet test` 运行中生效）。

## 设计决策

### 为什么用生成器驱动器而非快照/Verifier 框架

直接用 `CSharpGeneratorDriver` 驱动生成器，依赖最少，且能在同一测试中同时断言生成源文本、生成器诊断与合并编译结果。这与仓库统一使用的 xUnit v3 无缝配合，无需引入额外的验证框架依赖。

### 为什么用运行时受信平台程序集作为引用，且排除 Origo.* 程序集

测试编译以当前运行时的 `TRUSTED_PLATFORM_ASSEMBLIES` 作为元数据引用，使内存编译能解析任意 BCL 用法（`BitConverter`、`Unsafe`、`ModuleInitializer` 等），无需固定的引用程序集包。其中 `Origo.*` 程序集被显式排除：性能基准引用了真实的 `Origo.Core`，使其进入测试进程的受信平台程序集列表，而生成器驱动测试用内嵌源 scaffold 模拟 `Origo.Core.Snd.Metadata` 类型——若内存编译同时引用真实 `Origo.Core`，同名类型会冲突（CS0433）。

### 为什么 Adapter 用例引用独立宿主程序集并声明 InternalsVisibleTo

生成器通过 `TypedData` 所属程序集判定 Home/Adapter 模式。Adapter 用例将 `TypedData` 定义放入被引用的宿主程序集，使当前编译被识别为适配层；宿主程序集声明 `InternalsVisibleTo` 让生成的适配层代码可访问 `TypedData` 的内部字段，与 Origo.Core/Origo.GodotAdapter 的真实关系一致。

### 为什么性能基准只用 public API、采用宽松阈值并独立运行

性能基准引用真实 `Origo.Core` 但仅使用 `TypedData` 的 public API（显式转换运算符、`TryGetXxx`、`TryGetString`、`Data`、`FromObject`），覆盖多种值类型与引用类型 `string`，因此无需向测试项目开放 Core 内部成员（`TypedDataFactory<T>` 等内部类型不在基准范围内）。

基准是宽松的：不要求生成路径快于无优化装箱基线，只断言其不超过基线的固定倍数（8×，并对低于 1ms 的不可靠基线跳过比率）且单基准有总时长上限，目的是守住"不出现严重性能退化/卡死"，而非锁定绝对性能数字。

为抵抗 OS 时间片轮转与 GC 带来的测量噪声，每个基准使用固定容量池（位掩码寻址，内存恒定）、较大的迭代次数（使单轮耗时跨多个时间片）、一轮 warmup 加多轮计时并对两侧各取最小耗时（剔除被抢占/GC 的离群轮）。

基准标记 `[Trait("Category","Benchmark")]`，从 `ci.sh` 的全量测试运行中以 `--filter "Category!=Benchmark"` 排除，改由独立步骤 `scripts/benchmark.sh`（以 detailed logger）运行一次：既打印比对表格，又执行宽松断言，避免基准被运行两次。`scripts/benchmark.sh` 在同一步骤中还运行 Core 的[真实模拟性能基准](../Origo.Core.Tests/Benchmarks.md)（字典查找/插入、观察者通知、异构字典迭代等贴近使用的场景）。

---

> [↑ 回到 Origo.manual](../README.md)
