<!-- docsync-pair: benchmarks/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 性能基线

> [↑ 回到 Origo 手册](../README.zh.md)

## 概述

Origo 框架的性能基线数据与设计权衡分析，覆盖 TypedData 内联存储、实体生命周期、Observer 拓扑、DataSourceNode 树、Blackboard、Save 持久化、并发队列、随机数、Strategy 性能等子系统。

## 包含文件

| 文件 | 说明 |
|------|------|
| `baseline.zh.md` / `baseline.en.md` | Origo 框架各子系统的性能基线数据与设计权衡分析 |
| `README.md` | 自动生成的导航中枢（列出本目录全部文档，禁止手动编辑） |

## 基准测试文件

| 文件 | 被测模块 | 说明 |
|------|---------|------|
| `Origo.SourceGeneration.Tests/Benchmarks/TypedDataGeneratedBenchmarkTests.cs` | 源生成 | TypedData 内联 vs 装箱的纯净微基准：值类型 / 引用类型读写、混合分派 |
| `Origo.Core.Tests/Benchmarks/TypedDataRealWorldBenchmarkTests.cs` | TypedData | 真实 SND 路径模拟：字典查找/插入、数值强转链、观察者通知、异构迭代 |
| `Origo.Core.Tests/Benchmarks/EntityLifecycleBenchmarkTests.cs` | 实体生命周期 | 实体创建+AfterSpawn、帧处理缩放、BuildMetaData 序列化吞吐 |
| `Origo.Core.Tests/Benchmarks/ObserverTopologyBenchmarkTests.cs` | Observer 拓扑 | Mount/Unmount 绑定数量缩放 |
| `Origo.Core.Tests/Benchmarks/DataSourceNodeBenchmarkTests.cs` | DataSourceNode | 树构建、SHA-256 哈希、As<T> 类型分派 |
| `Origo.Core.Tests/Benchmarks/BlackboardBenchmarkTests.cs` | Blackboard | 批量 SetValue/TryGet、序列化往返 |
| `Origo.Core.Tests/Benchmarks/SavePayloadBenchmarkTests.cs` | Save 持久化 | Payload 哈希计算、Write/Read 往返、Snapshot 往返 |
| `Origo.Core.Tests/Benchmarks/ConcurrentActionQueueBenchmarkTests.cs` | 并发队列 | Enqueue+ExecuteAll 缩放、Enqueue 吞吐 |
| `Origo.Core.Tests/Benchmarks/RandomGeneratorBenchmarkTests.cs` | 随机数 | XorShift128+ NextUInt64/NextInt64/NextInt32 吞吐 |
| `Origo.Core.Tests/Snd/Strategy/SndStrategyPerformanceTests.cs` | Strategy | 策略池 Get/Release、Process 帧处理缩放、TriggerAll 分配 |
| `Origo.GodotAdapter.Tests/Serialization/GodotTypedDataPerformanceTests.cs` | Godot 适配器 | Godot 注册类型的 TypedData 读写/转换吞吐、混合实体模拟 |

## 运行

```bash
bash scripts/benchmark.sh
```

该脚本依次运行三套基准（均标记 `[Trait("Category","Benchmark")]`，从 `test.sh` 排除），并在比对前检查同一轮运行中所有 `BENCH` metric key 唯一；重复 key 直接失败：

- **SG 纯净微基准** — `Origo.SourceGeneration.Tests`
- **Core 基准** — `Origo.Core.Tests`（TypedData 真实模拟 + 实体生命周期 + Observer 拓扑 + DataSourceNode + Blackboard + Save + 并发队列 + 随机数 + Strategy 性能）
- **Godot 适配器基准** — `Origo.GodotAdapter.Tests`

---

[↑ 回到 Origo 手册](../README.zh.md)
