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
| `TypedDataGeneratorTests.cs` | 生成器行为测试：Home/Adapter 模式输出、两存储模型、`ORIGOSG001`–`ORIGOSG004` 诊断、生成确定性与增量管线 |
| `Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | 生成产物性能基准：多值类型 + `string` 的写/读/混合分发，生成的内联 `TypedData` vs 无优化装箱；固定池 + 大迭代 + 多轮取最小降噪，宽松阈值 + 比对表格，并对每侧实测分配（`GC.GetAllocatedBytesForCurrentThread`，置于独立 `NoInlining` 方法以免污染计时） |
| `TestSupport/PerfReporter.cs` | 性能比对表格输出器（同时写控制台与 xUnit 测试输出） |

## TypedDataGeneratorTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Home_Primitives_GeneratesExpectedMembers_AndCompiles` | 宿主模式注册系统基础类型，生成 `KindMap`/`TryGetInt32`/`AsInt32`/`explicit operator`/`TypedDataFactory<T>`/`TypedDataHomeKindRegistration` + `[ModuleInitializer]`，合并编译零错误 | Origo.SourceGeneration |
| `Home_StringStoredViaRefSlot` | `string` 通过 `_ref` 槽存取（`AsString() => (string?)_ref`、`case 13: return td._ref`） | Origo.SourceGeneration |
| `Adapter_ValueAndRefTypes_UseRefSlot_AndCompiles` | 适配层非系统值类型与引用类型统一走 `_ref`，生成 `TypedDataLayeredExtensions`、`RegisterKind`、Converter/TypeMap 分支，合并编译零错误 | Origo.SourceGeneration |
| `Generation_IsDeterministic` | 相同输入两次运行产出完全一致的源文本 | Origo.SourceGeneration |
| `StartKind_OffsetIsHonored_AndNumberingIsSequential` | `StartKind` 偏移生效（128/129），且按声明顺序递增 | Origo.SourceGeneration |
| `OverlappingStartKinds_SameType_Deduplicated` | 同一类型在重叠 `StartKind` 组中重复声明被去重，无诊断、无编译错误 | Origo.SourceGeneration |
| `Incremental_SameInputTwice_ProducesIdenticalOutput` | 同一输入连续运行两次，生成源逐项一致 | Origo.SourceGeneration |
| `Incremental_SameInputTwice_NoAdditionalOutputs` | 同一输入运行三次，首次与第三次生成源数量与内容一致（无多余输出） | Origo.SourceGeneration |
| `Incremental_UnrelatedCodeChange_GeneratedOutputUnchanged` | 追加无关注释不改变生成输出 | Origo.SourceGeneration |
| `Incremental_NoAttribute_ThenAddAttribute_ProducesNewOutput` | 从无特性到加上特性，输出从空变为非空 | Origo.SourceGeneration |
| `Incremental_HasAttribute_ThenRemoveAttribute_OutputDisappears` | 从有特性到移除特性，输出从非空变为空 | Origo.SourceGeneration |
| `Incremental_AddTypeToExistingAttribute_OutputChanges` | 向既有特性追加类型，输出增加对应类型成员（`Single`） | Origo.SourceGeneration |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Home_UnsupportedValueType_ReportsORIGOSG002_ButStillGeneratesValidTypes` | 宿主组注册不可内联值类型（`decimal`） | 报告 `ORIGOSG002`（Error），仅剔除不支持类型，`int` 仍正常生成且合并编译零错误 |
| `Adapter_SystemPrimitive_ReportsORIGOSG001` | 适配层组注册系统基础类型（`int`） | 报告 `ORIGOSG001`（Error），剔除该基元，不产出其内联访问器 |
| `KindPastByteRange_ReportsORIGOSG003_IncludingWrapToNonZero` | `StartKind` 偏移使 Kind 超出 byte 范围（256/257，257 会回绕为 1 造成碰撞） | 报告 `ORIGOSG003`（Error），剔除越界类型，范围内类型（`Byte=255`）仍生成 |
| `OverlappingStartKindRanges_ReportORIGOSG004_AndDropCollidingTypes` | 两组将同一 Kind 1 分配给不同类型（`int`/`long`） | 报告 `ORIGOSG004`（Error），剔除碰撞的两个类型 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Home_NoAttribute_ProducesNoOutput` | 无 `SndInlineTypes` 特性 | 不产出任何源，无诊断 |
| `Home_OnlyReferenceTypes_NoInlineMethods` | 仅注册引用类型（`string`） | 生成 `KindMap`/`AsString`，但不产出 `explicit operator`/`_inlineBits` 等内联机制 |
| `Home_DoesNotEmitSilentStubHelpers` | 宿主模式生成（回归守卫） | 永不生成 `BitsFrom`/`ReadBitsAs`/`Pack`/`return default;` 等静默桩辅助 |
| `Adapter_DoesNotEmitInlineHelpers` | 适配层模式生成（回归守卫） | 不产出 `ReadBitsAs`/`BitsFrom`/`_inlineBits` |

## Benchmarks/TypedDataGeneratedBenchmarkTests 测试详情

> 标记 `[Trait("Category","Benchmark")]`（类级），仅由 `scripts/benchmark.sh` 运行；`test.sh` 全量测试以 `--filter "Category!=Benchmark"` 排除。每个用例先校验生成路径与装箱基线命中数一致，再断言生成路径不超过基线 8× 且单基准总耗时低于上限，并打印比对表格。

### 正确路径（性能基准）

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ValueTypes_WriteThroughput_GeneratedOperator_vs_BoxedClass` | 值类型（`int`/`long`/`float`/`double`/`bool`/`char`）写入：生成的 `explicit operator` vs 装箱类，在预算内 | Origo.SourceGeneration |
| `ValueTypes_ReadThroughput_GeneratedKind_vs_BoxedIsT` | 值类型读取：生成的 `TryGetXxx`（Kind 分发）vs 装箱 `Data is T`，命中数一致且在预算内 | Origo.SourceGeneration |
| `ReferenceType_String_GeneratedRefSlot_vs_BoxedClass` | `string` 通过 `_ref` 槽的写入与读取 vs 装箱类 | Origo.SourceGeneration |
| `StringRead_IsString_vs_BoxedIsT` | `string` 的 `IsString` 判定 vs 装箱 `Data is string` | Origo.SourceGeneration |
| `MixedDispatch_GeneratedKind_vs_BoxedIsT` | 混合类型池（int/float/bool/string/double）的 Kind 分发 vs 装箱 `is T`，命中数一致且在预算内 | Origo.SourceGeneration |

## 测试辅助策略

| 设施 | 类型 | 用途 |
|------|------|------|
| `GeneratorTestHarness` | 内部静态类 | 构造内存 `CSharpCompilation`（以受信平台程序集为引用，排除 `Origo.*`），运行 `TypedDataGenerator`，暴露生成源、生成器诊断、合并编译错误；并提供 `CreateTrackedDriver`/`RunIncremental` 用于增量管线断言 |
| `GeneratorOutput` | 内部 record | 封装生成源数组、生成器诊断、合并编译错误，提供 `AllGeneratedText` 与 `HasGeneratorDiagnostic(id)` |
| `PerfReporter` | 公共类 | 性能比对表格输出器（同时写控制台与 xUnit 测试输出），由基准用例通过 `PerfReporter.ForTest` 注入 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 生成器对畸形/部分构造的 `SndInlineTypes` 特性（如 `null` 类型参数、非类型实参）的处理未直接断言 | 边界输入的诊断行为未覆盖 | Origo.SourceGeneration |
| 同一编译中 Home 与 Adapter 模式共存的判定路径未单独验证 | 模式判定的混合场景未覆盖 | Origo.SourceGeneration |
| 性能基准未覆盖适配层非系统值类型（如 Godot 类型）的读写吞吐，仅覆盖系统基元 + `string` | 适配层 `_ref` 路径性能特征未基准化 | Origo.SourceGeneration |

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

基准标记 `[Trait("Category","Benchmark")]`，从 `test.sh` 的全量测试运行中以 `--filter "Category!=Benchmark"` 排除，改由独立步骤 `scripts/benchmark.sh`（以 detailed logger）运行一次：既打印比对表格，又执行宽松断言，避免基准被运行两次。`scripts/benchmark.sh` 在同一步骤中还运行 Core 的[真实模拟性能基准](../Origo.Core.Tests/Benchmarks.md)（字典查找/插入、观察者通知、异构字典迭代等贴近使用的场景）。

---

> [↑ 回到 Origo.manual](../README.md)
