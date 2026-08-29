<!-- docsync-pair: Origo.Core.Tests/Benchmarks -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 性能基准 (Benchmarks)

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.zh.md) · [↔ SG 纯净微基准: Origo.SourceGeneration.Tests](../Origo.SourceGeneration.Tests/README.zh.md)

## 被测行为概览

这套基准覆盖 Origo 框架核心子系统的吞吐与分配特征。其中 TypedData 相关基准在贴近真实使用的路径上，对比源生成的 `TypedData`（内联存储 + Kind 分派）与无优化的「装箱进 `Dictionary<string, object>`」实现的吞吐；其余基准测量实体生命周期、Observer 拓扑、DataSourceNode 树构建/遍历、Blackboard 读写、Save 持久化、并发队列、随机数生成和 Strategy 池等子系统的性能特征。

基准标记 `[Trait("Category","Benchmark")]`，从 `test.sh` 的全量测试运行中以 `--filter "Category!=Benchmark"` 排除，改由独立步骤 `scripts/benchmark.sh` 运行一次。该脚本同时运行本套件、[SG 纯净微基准](../Origo.SourceGeneration.Tests/README.zh.md)和 [Godot 适配器基准](../Origo.GodotAdapter.Tests/Serialization.zh.md)，并在比对基线前检查同一轮运行中所有 `BENCH` metric key 唯一；重复 key 直接失败。

> **性能数值不记录在本文档。** 易变的绝对吞吐与倍率快照见权威基线 [benchmarks/baseline.md](../benchmarks/baseline.zh.md)，本文档只描述被测能力与设计意图。

## 包含文件

| 文件 | 职责 |
|------|------|
| `Benchmarks/TypedDataRealWorldBenchmarkTests.cs` | 五个 TypedData 真实模拟基准：字典查找/插入、数值强转链、观察者通知、异构字典迭代 |
| `Benchmarks/EntityLifecycleBenchmarkTests.cs` | 实体创建+AfterSpawn 缩放、帧处理（ProcessAll）缩放、SaveSingle 吞吐 |
| `Benchmarks/ObserverTopologyBenchmarkTests.cs` | ObserverTopology Mount/Unmount 绑定数量缩放 |
| `Benchmarks/DataSourceNodeBenchmarkTests.cs` | DataSourceNode 树构建、SHA-256 哈希计算、As<T> 类型分派吞吐 |
| `Benchmarks/BlackboardBenchmarkTests.cs` | 内存 Blackboard 的 SetValue/TryGet 批量吞吐、SerializeAll+DeserializeAll 往返 |
| `Benchmarks/SavePayloadBenchmarkTests.cs` | SavePayload 哈希计算、WriteToCurrent+ReadFromCurrent 往返、Snapshot 往返 |
| `Benchmarks/ConcurrentActionQueueBenchmarkTests.cs` | ConcurrentActionQueue Enqueue+ExecuteAll 缩放、Enqueue 吞吐 |
| `Benchmarks/RandomGeneratorBenchmarkTests.cs` | XorShift128+ NextUInt64/NextInt64/NextInt32 吞吐 |
| `Snd/Strategy/SndStrategyPerformanceTests.cs` | 策略池 Get/Release 往返、Process 帧处理缩放、TriggerAll 分配 |
| `Origo.TestSupport/Reporting/PerfReporter.cs` | 性能比对表格输出器（`Report`、`Compare`、`CompareTable`、`ReportTable`），同时写控制台与 xUnit 测试输出；同一进程内重复 `BENCH` metric key 抛 `InvalidOperationException` |
| `TestSupport/PerfReporterMetricKeyUniquenessTests.cs` | 钉定 PerfReporter 的 metric key 唯一性守卫：重复 key 必须立即失败 |

## 基准方法

### TypedDataRealWorldBenchmarkTests

| 基准方法 | 模拟的真实路径 | 对比内容 |
|---------|---------------|---------|
| `DictLookup_TryExtract_vs_BoxedDict` | `SndDataManager.TryGetData<T>`：字典查找命中后用 `TryGetXxx` 提取 | `Dictionary<string,TypedData>` + `TryGetXxx` vs `Dictionary<string,object>` + `is T` |
| `DictInsert_FactoryCreate_vs_BoxedDict` | `SndDataManager.SetData<T>`：构造值并写入字典 | 生成工厂 `Create`/隐式转换 + 插入 vs 装箱插入 |
| `MultiTypeExtractionChain_Generated_vs_Boxed` | `TryGetNumeric` 跨数值类型逐一尝试读取 | 生成 `TryGetSingle→TryGetInt32→TryGetInt64→TryGetDouble` 链 vs `is float→is int→is long→is double` 链 |
| `ObserverNotify_Generated_vs_Boxed` | 观察者回调传递新旧值并判型 | 传递 `(TypedData, TypedData)` + `TryGetString` vs 传递 `(object?, object?)` + `is string` |
| `HeterogeneousDictIteration_GeneratedData_vs_BoxedDict` | 遍历异构数据字典，逐项经 `TypedDataObjectConverter.ToObject` 转为 `object` | 生成 `ToObject`（`object` 化）vs 纯 `object` 直通 |

### EntityLifecycleBenchmarkTests

| 基准方法 | 测试内容 |
|---------|---------|
| `EntityCreation_ScalingByEntityCount` | 100/500/2000 实体创建 + `FireAfterSpawnHooks` 的吞吐与分配 |
| `FrameProcessing_ScalingByEntityAndStrategyCount` | 10e×1s / 50e×5s / 200e×10s 配置下 200 帧 ProcessAll 吞吐 |
| `EntitySaveSingle_ScalingByEntityCount` | 10/100/500 实体 `BuildMetaData`（序列化元数据构建）吞吐 |

### ObserverTopologyBenchmarkTests

| 基准方法 | 测试内容 |
|---------|---------|
| `ObserverMount_ScalingByBindingCount` | 10/50/200 绑定 Mount 吞吐 |
| `ObserverUnmount_ScalingByBindingCount` | 10/50/200 绑定 Unmount 吞吐 |

### DataSourceNodeBenchmarkTests

| 基准方法 | 测试内容 |
|---------|---------|
| `TreeBuild_ScalingByDepthAndWidth` | d2w5/d3w8/d4w8 树构建的吞吐与分配 |
| `TreeTraversalAndHashCompute` | d3w8/d4w8 树 `ComputeSha256Hash` 的吞吐 |
| `AsT_TypeDispatchThroughput` | 500k 次 × 100 元素数组上的 Number/Text/Bool 类型分派 |

### BlackboardBenchmarkTests

| 基准方法 | 测试内容 |
|---------|---------|
| `SetValue_BulkWrite_ThroughputByType` | Int32/Single/String/Boolean 各 100k 次 SetValue 的吞吐与分配 |
| `TryGet_BulkRead_ThroughputByType` | Int32/Single/String/Boolean 各 500k 次 TryGet 的吞吐与分配 |
| `SerializeAllDeserializeAll_Roundtrip` | 100/500/1000 key 的 SerializeAll+DeserializeAll 往返吞吐 |

### SavePayloadBenchmarkTests

| 基准方法 | 测试内容 |
|---------|---------|
| `PayloadHashCompute_ScalingByEntityCount` | 10/100/500 entity `ComputePayloadHash` 吞吐 |
| `PayloadWriteAndRead_Roundtrip` | `WriteToCurrent` + `ReadFromCurrent` 往返 |
| `PayloadSnapshotWriteAndRead_Roundtrip` | `WriteSavePayloadToCurrentThenSnapshot` + `ReadSavePayloadFromSnapshot` 往返 |

### ConcurrentActionQueueBenchmarkTests

| 基准方法 | 测试内容 |
|---------|---------|
| `EnqueueAndExecuteAll_ScalingByActionCount` | 100/1000/10000 action `Enqueue`+`ExecuteAll` 吞吐 |
| `EnqueueThroughput_BulkInsert` | 1000/10000/50000 action `Enqueue` 批量吞吐 |

### RandomGeneratorBenchmarkTests

| 基准方法 | 测试内容 |
|---------|---------|
| `NextUInt64_Throughput` | 10M 次 `NextUInt64` 吞吐 |
| `NextFunctions_ThroughputComparison` | 5M 次 `NextUInt64`/`NextInt64`/`NextInt32` 对比 |

### SndStrategyPerformanceTests

| 基准方法 | 测试内容 |
|---------|---------|
| `StrategyPool_GetRelease_Throughput` | 100k 次 Get+Release 往返 |
| `StrategyManager_Process_StrategyCountScaling` | 1/5/10/20 策略 × 10k 帧 ProcessAll |
| `TriggerAll_AfterSpawn_AllocationByStrategyCount` | 1/10 策略 AfterSpawn TriggerAll 的分配量 |

每个基准的数据集混合 `int`/`float`/`bool`/`string`/`double` 五种类型（`i % 5` 轮换），以反映异构 SND 数据的真实分布。

## 测试辅助设施

`PerfReporter`（见 [Origo.Core.Tests README — 测试辅助设施](README.zh.md)）按统一表格格式打印「方法 / 迭代数 / 耗时 / 吞吐 / 分配」，并双通道输出到 `Console.Out` 与 `ITestOutputHelper`，确保 CI 与本地均可见结果。

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 不覆盖 GodotAdapter 注册类型（`Vector2`/`Vector3` 等）的真实场景吞吐 | 适配层多层分派性能不在本套件验证 | 由 [Origo.GodotAdapter.Tests/Serialization](../Origo.GodotAdapter.Tests/Serialization.zh.md) 的 `GodotTypedDataPerformanceTests` 单独覆盖 |
| 不覆盖并发/多线程读写 | 多线程下的争用与可见性未测 | 框架采用单线程帧模型（见 [手册根 README — 设计原则](../README.zh.md)） |
| 不覆盖按 Kind 直接分派的异构迭代替代路径 | 仅测 `ToObject`（`object` 化）这一条迭代路径 | [Snd/Metadata](../Origo.Core/Snd/Metadata/README.zh.md) 的 Kind 分派 |
| 绝对吞吐/倍率/分配均不作断言（仅打印 + 单基准时间上限） | 性能退化无法自动捕获，需人工对照基线 | [benchmarks/baseline.md](../benchmarks/baseline.zh.md) |

## 设计决策

### 为什么基准宽松：只设时间上限，不断言「更快」

本套件对每个基准仅断言单基准总耗时低于 8 秒上限（`AssertInCap`），**不**设固定倍率上限，也不要求生成路径快于装箱基线。真实路径叠加了字典查找、对象转换等成本，生成路径在部分读取场景本就允许慢于装箱基线（详见基线文件）。基准的目的是「守住不卡死、可长期跟踪相对趋势」，而非锁定绝对数字或强制不退化——后者会因机器与运行时差异频繁误报。

> 这与 [SG 纯净微基准](../Origo.SourceGeneration.Tests/README.zh.md) 略有不同：SG 套件额外断言生成路径不超过基线 8×；本套件因真实路径基线更复杂，只保留时间上限。

### 为什么标记 `Category=Benchmark` 并独立运行

基准迭代次数大、耗时长，若与受覆盖率门禁约束的测试一起跑会拖慢常规 CI，且不应被统计进行覆盖率。标记 `[Trait("Category","Benchmark")]` 后由 `test.sh` 以 `--filter "Category!=Benchmark"` 排除，改由 `scripts/benchmark.sh` 在独立步骤运行一次，既打印比对表格又执行宽松断言，避免被运行两次。

### 为什么用固定数据集 + 多轮取最小降噪

为抵抗 OS 时间片轮转与 GC 带来的测量噪声，每个基准使用固定容量的字典/数据集（内存恒定）、较大的迭代次数（使单轮耗时跨多个时间片），并以 1 轮 warmup 加多轮计时、对生成与装箱两侧各取最小耗时，剔除被抢占/GC 的离群轮。

### 为什么分配测量放在独立 NoInlining 方法里

每个基准在计时轮之外，对生成/装箱两侧各跑一次专用测量轮，取 `GC.GetAllocatedBytesForCurrentThread()` 前后差值作为该轮分配。测量循环被抽到独立的 `[MethodImpl(NoInlining)]` 方法中：若内联进计时方法体（或用捕获局部变量的 lambda），会改变计时循环的代码生成（闭包字段间接、循环对齐），从而污染吞吐数字。放在独立方法里使计时循环保留未插桩版的代码生成，分配与时间互不干扰。

### 为什么基准与真实消费者一致：经 `TypedDataObjectConverter` 的 object 化路径

`TypedData` 的全部 `AsXxx` 访问器与对象转换器均为 `internal`（测试项目经 `InternalsVisibleTo` 访问）。异构迭代基准经 internal `TypedDataObjectConverter.ToObject` 度量"编译期未知类型的 object 化"冷路径——这是序列化、控制台、`ToString` 等冷路径的真实调用形态；热/温路径（数据变更信号处理、加载校验）使用零装箱的 `TryGetXxx`，见 [Snd/Metadata](../Origo.Core/Snd/Metadata/README.zh.md)。

---

> [↑ 回到 Origo.Core.Tests](README.zh.md)
