<!-- docsync-pair: benchmarks/baseline -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Origo 性能基线

> [↑ 回到 Origo.manual](../README.zh.md)

> Origo 框架各子系统的性能现状快照，作为后续性能优化的权威对照来源。
> 数值与运行环境、运行时版本强相关，跨机器不可直接比较；优化前后须在**同一环境同一运行时**下复测。

> 当前 `baseline.json` 仅保留 metric key 未变化且仍可对应的条目；重新命名或重新设计测量路径的指标暂无基线条目，需在基线机运行 `bash scripts/benchmark.sh --update-baseline` 后回填并提交。

## 复现

在 origo 源码仓根目录运行：

```bash
bash scripts/benchmark.sh
```

该脚本依次运行三套基准（均标记 `[Trait("Category","Benchmark")]`，从 `test.sh` 排除，仅此处运行一次）：

- **SG 纯净微基准** — `Origo.SourceGeneration.Tests/Benchmarks/TypedDataGeneratedBenchmarkTests.cs`
- **Core 子系统基准** — `Origo.Core.Tests`（TypedData 真实模拟 + 实体生命周期 + Observer 拓扑 + DataSourceNode + Blackboard + Save + 并发队列 + 随机数 + Strategy 性能）
- **Godot 适配器基准** — `Origo.GodotAdapter.Tests`（Godot 注册类型的 TypedData 读写/转换吞吐）

## 回归门禁（benchmark.sh 比对）

`scripts/benchmark.sh` 会把每次运行的测量值与 `docs/benchmarks/baseline.json` 比对（机器可读基线，由 `PerfReporter.EmitMetric` 输出的 `BENCH|kind|label|side|ops|alloc` 行生成）：

- **仅当运行机器与基线记录的 `machine_id` 相同时执行回归门禁**：吞吐下降超 50%（限 min-of-rounds 测量 `CompareTable`/`Compare`/`Report`）与分配增长超 20% 均在此时判定失败
- **机器不匹配时跳过全部数值门禁**（CI runner 是随机机器、全新 VM，吞吐与分配都受 CPU 调频、tiered-JIT 内联决策与运行时构建影响，跨机器均不可比）：benchmark 步骤仅作冒烟测试，确认基准代码可运行
- 本地 `scripts/ci.sh` 在同机器运行，可捕捉真实的吞吐与分配退化
- 确认改进或环境变更后，运行 `bash scripts/benchmark.sh --update-baseline` 刷新基线并提交

## 采样元信息

| 项 | 值 |
|----|----|
| CPU | AMD Ryzen 7 9700X（8C/16T，基频 3.80 GHz） |
| 内存 / OS | 30 GiB / Ubuntu 26.04 LTS（Linux 7.0.0-30-generic） |
| .NET | SDK 10.0.400，运行时 10.0.11（测试目标 `net10.0`） |
| 构建 | `Release` |
| 采样 | 单次运行（TypedData 真实模拟 10 轮 warmup；min-of-rounds 内部取 `min of 5`） |

> 下方数值表格为当前基线快照的参考展示；**回归门禁的数据源是 `docs/benchmarks/baseline.json`**（随机器与运行时变化由 `--update-baseline` 刷新）。

## 方法学

- **时间**：固定容量池 + 位掩码寻址（内存恒定）、大迭代次数（单轮跨多个 OS 时间片）、10 轮 warmup + 多轮取两侧各自最小耗时（剔除被抢占/GC 的离群轮）。
- **分配**：每个基准对每一侧各跑一次专用 `[MethodImpl(NoInlining)]` 测量轮，取 `GC.GetAllocatedBytesForCurrentThread()` 前后差值。测量置于独立方法，使其循环体不与计时循环共用代码生成、不影响吞吐。
- **Alloc 列**：每轮（即表中迭代数对应的一轮）的实测分配，格式「生成 / 装箱」。

> **「Alloc」与相对趋势是最可靠的判据**：跨 4 轮逐字一致。绝对吞吐受 CPU 调频与超平凡循环的代码对齐影响存在抖动（见末尾效度局限）。

## SG 纯净微基准

### 写入吞吐 — 生成 operator vs 装箱 class（2,000,000 迭代）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 | Alloc 生成/装箱 |
|------|--------------:|--------------:|:----:|:----:|----------------|
| Int32 | 920.05 | 72.32 | 12.72x | 生成 | 0 B / 106.81 MB |
| Int64 | 899.36 | 71.11 | 12.65x | 生成 | 0 B / 106.81 MB |
| Single | 883.70 | 72.01 | 12.27x | 生成 | 0 B / 106.81 MB |
| Double | 888.49 | 71.61 | 12.41x | 生成 | 0 B / 106.81 MB |
| Boolean | 905.63 | 72.01 | 12.58x | 生成 | 0 B / 106.81 MB |
| Char | 921.40 | 72.11 | 12.78x | 生成 | 0 B / 106.81 MB |
| String（ref slot） | 602.90 | 130.03 | 4.64x | 生成 | 0 B / 61.04 MB |

### 读取吞吐 — 生成 TryGet/Kind vs 装箱 `is T`（10,000,000 迭代，两侧 Alloc 均 0 B）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 | 稳定性 |
|------|--------------:|--------------:|:----:|:----:|--------|
| Int32 | 551.94 | 656.18 | 0.84x | 装箱 | 稳 |
| Int64 | 554.01 | 652.83 | 0.85x | 装箱 | 稳 |
| Single | 564.36 | 654.69 | 0.86x | 装箱 | 稳 |
| Double | 560.50 | 597.51 | 0.94x | 装箱 | 稳 |
| Boolean | 527.18 | 651.17 | 0.81x | 装箱 | 稳 |
| Char | 551.92 | 595.73 | 0.93x | 装箱 | 稳 |
| String（`TryGetString`） | 416.40 | 659.57 | 0.63x | 装箱 | 高方差 |
| String（`IsString`） | 572.32 | 660.44 | 0.87x | 装箱 | 稳 |

### 混合分派 — 生成 Kind switch vs 装箱 `is T`（10,000,000 迭代，两侧 0 B）

| 场景 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| 混合分派（int/float/bool/string/double） | 1249.41 | 815.57 | 1.53x | 生成 |

## Core 真实模拟基准

### 异构字典迭代（2,048,000 次 `ToObject` 读取）

| 场景 | 生成 ToObject (Mops/s) | 装箱迭代 (Mops/s) | 倍率 | 胜方 | Alloc 生成/装箱 |
|------|--------------------:|------------------:|:----:|:----:|----------------|
| 异构字典 `ToObject` 迭代 | 418.27 | 3024.22 | 7.2x | 装箱 | 37.49 MB / 0 B |

> 这是**合成最坏情况**：internal `TypedDataObjectConverter.ToObject` 返回 `object`，值类型每次读取经 `ToObject` 重新装箱（数据集 ~80% 为值类型 → 每轮 37.49 MB）。生产无此调用形态（见「设计权衡」）。

### 工厂构造 + 字典插入（500,000 迭代）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 | Alloc 生成/装箱 |
|------|--------------:|--------------:|:----:|:----:|----------------|
| String | 177.12 | 194.73 | 0.91x | 装箱 | 23.53 MB / 14.97 MB |
| Int32 | 209.13 | 138.85 | 1.51x | 生成 | 23.53 MB / 26.42 MB |
| Single | 206.48 | 129.38 | 1.60x | 生成 | 23.53 MB / 26.42 MB |
| Boolean | 207.56 | 130.85 | 1.59x | 生成 | 23.53 MB / 26.42 MB |

> 值类型插入装箱侧多出 ~12 MB 装箱（26.42 vs 23.53），生成更快且更省。String 插入生成侧反而略多分配（23.53 vs 14.97 MB）：`Dictionary<string,TypedData>` 每 entry 内嵌 24 字节结构体，后备数组大于 `Dictionary<string,object>` 的 8 字节引用。

### 观察者通知（2,000,000 迭代，两侧 0 B）

| 场景 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| Observer notify (old,new) + 判型 | 1863.06 | 1842.30 | 1.01x | 生成 |

> 经 `TypedData` 传递（非 `object`），用 `TryGetString` 判型，零装箱，与装箱 `is string` 持平。

### 字典查找 TryExtract（2,000,000 迭代，两侧 0 B）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 | 稳定性 |
|------|--------------:|--------------:|:----:|:----:|--------|
| String | 196.00 | 185.94 | 1.05x | 生成 | 稳 |
| Int32 | 125.16 | 140.90 | 0.89x | 装箱 | 稳 |
| Single | 125.27 | 127.55 | 0.98x | 装箱 | 稳 |
| Boolean | 125.17 | 122.13 | 1.02x | 生成 | 稳 |

### 多类型强转链 float→int→long→double（2,000,000 迭代，int payload，两侧 0 B）

| 场景 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| 数值强转链 | 261.27 | 251.89 | 1.04x | 生成 |

## 性能现状概览

- **值类型**：写入约 12.3–12.8x、混合分派约 1.53x、字典构造+插入 1.51–1.60x（String 生成略慢于装箱）；纯微基准单读当前装箱更快（0.81–0.94x），字典查找已接近持平。生成路径保持零装箱，但相对优势随 CPU 代际变化。
- **string**：写入 4.64x 超装箱；`TryGetString` 数组读取约 1.58x 慢，`IsString` 路径在当前 CPU 上也约 1.15x 慢于装箱（结构性，见下）。
- **DictLookup**：String/Boolean 生成略快（1.05x/1.02x），Int32/Single 接近持平（0.89x/0.98x）。
- **观察者通知**：生成 1.01x 优于装箱。
- 字符串读取的两条路径当前均以装箱略快；该差距来自 `TypedData` 24 字节布局的缓存行为，而非生成代码本身的指令开销（见下）。

## 设计权衡与已评估方向

记录现状为何如此、做了哪些取舍，以及**已验证无效、不应重试**的方向，避免后续迭代走老路。

- **`TryGetString` 用 `Unsafe.As<string>`（受 `_kind == String` 守卫）而非 `(string)_ref`**：守卫已保证 `_ref` 是 string，`castclass` 是冗余的；且 `castclass` 可抛异常，会阻断 JIT 对「结果被丢弃/循环不变」的 `TryGetString` 调用的消除与外提。在**操作绑定**的 `TryGetString` 路径（如观察者通知）上，去掉它使生成侧与装箱 `is string` 持平。

- **【已验证无效，勿重试】去 `castclass` 对「数组 string 读取」无可测收益**：同一改动在缓存绑定的数组读取上仅带来噪声内的变化（中位约 +2%），不改变装箱约 1.58x 更快的结论。该路径瓶颈是**缓存而非指令**——`_ref` 在结构体偏移 16，在 24 字节步长 + 64 字节缓存行下比值类型读取的 `_inlineBits`（偏移 8）更易跨缓存行。指令级微调无法改善，勿在此方向反复尝试。

- **【已评估不划算，勿尝试】把 `TypedData` 结构体压到 16 字节**：当前 24 字节（`byte _kind` + `long _inlineBits` + `object? _ref`）是 GC 安全设计的下限——`long` 与托管引用槽不能重叠（GC 需独立扫描引用），`_kind` 字节无空闲位可塞。压到 16 字节须牺牲 `long`/`double` 的满 64 位内联，或引入额外分支/类型查找（多半负收益），且改动 `internal` 布局（被生成代码与测试经 `InternalsVisibleTo` 依赖）。值类型单读与 DictLookup 在 0.89x–1.24x 之间波动、多数接近持平的结构性差距即源于此，已接受。

- **`ToObject`（object?）的装箱只落在冷路径**：internal `TypedDataObjectConverter.ToObject` 对值类型装箱，服务于编译期无法得知类型的冷路径（按 `DataType` 的序列化、控制台、`ToString`），这些路径固有需要 `object`。框架内热/温路径（数据变更信号处理、加载校验等）一律用零装箱的 `TryGetXxx`。**「异构 `ToObject` 迭代」基准（约 7.2x、37.49 MB）是合成最坏情况，不对应任何真实生产热路径**；测试项目经 `InternalsVisibleTo` 访问该 internal 转换器以度量此冷路径。取舍与推荐用法见 [Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.zh.md)。

## 效度局限

1. **CPU 动态调频（当前 scaling ≈ 88%）** 引入运行间抖动；针对 ≤ 1.3x 的边际项与最快循环（异构迭代装箱侧 6.3–8.4x、写入），建议固定频率/性能模式后复测。
2. **绝对吞吐受代码对齐影响**：写入等超平凡循环对方法布局/对齐敏感，存在 ~±8% 抖动；**分配量与相对趋势不受此影响**，应作为主要判据。
3. **运行时为 .NET 10.0.11**，与测试目标 `net10.0` 一致；结论须在同一运行时下复测。
4. **微基准取 min-of-rounds**，偏向理想 JIT 稳态；真实模拟套件更具代表性。
5. 高方差项：String `TryGetString` 读取、混合分派、异构迭代装箱侧、值类型插入装箱侧；对该些项下结论前应增加采样轮数（≥ 8 轮）收敛。

## 子系统性能基线

> 以下为框架核心子系统的性能快照，按模块分组。每种基准取 min-of-rounds 或单次测量。

### 实体生命周期

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| 100 entities create+spawn | 100 | 180.60 us | 553.71 Kops/s | 374.75 KB |
| 500 entities create+spawn | 500 | 867.80 us | 576.17 Kops/s | 1.82 MB |
| 2000 entities create+spawn | 2,000 | 3.47 ms | 575.71 Kops/s | 7.29 MB |

### 帧处理

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| 10 entities × 1 strategy, 200 frames | 2,000 | 114.50 us | 17.47 Mops/s | 600 B |
| 50 entities × 5 strategies, 200 frames | 50,000 | 1.27 ms | 39.52 Mops/s | 3.16 KB |
| 200 entities × 10 strategies, 200 frames | 400,000 | 8.21 ms | 48.70 Mops/s | 20.35 KB |

### 实体存档 (SaveSingle)

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| 10 entities SaveSingle | 10 | 46.30 us | 215.98 Kops/s | 9.57 KB |
| 100 entities SaveSingle | 100 | 104.00 us | 961.54 Kops/s | 95.35 KB |
| 500 entities SaveSingle | 500 | 264.90 us | 1.89 Mops/s | 476.60 KB |

### Observer 拓扑

#### Mount

| 场景 | 操作数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Mount × 10 | 10 | 59.20 us | 168.92 Kops/s | 23.60 KB |
| Mount × 50 | 50 | 71.40 us | 700.28 Kops/s | 112.49 KB |
| Mount × 200 | 200 | 435.30 us | 459.45 Kops/s | 450.70 KB |

#### Unmount

| 场景 | 操作数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Unmount × 10 | 10 | 105.20 us | 95.06 Kops/s | 19.79 KB |
| Unmount × 50 | 50 | 62.50 us | 800.00 Kops/s | 94.29 KB |
| Unmount × 200 | 200 | 389.70 us | 513.22 Kops/s | 374.63 KB |

### DataSourceNode 树

| 场景 | 节点数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Tree build d=2 w=5 | 31 | 159.00 us | 194.97 Kops/s | 40.16 KB |
| Tree build d=3 w=8 | 585 | 284.00 us | 2.06 Mops/s | 763.25 KB |
| Tree build d=4 w=8 | 4,681 | 2.69 ms | 1.74 Mops/s | 6.07 MB |
| SHA-256 hash d=3 w=8 | 585 | 4.52 ms | 129.33 Kops/s | 1.76 MB |
| SHA-256 hash d=4 w=8 | 4,681 | 7.36 ms | 635.74 Kops/s | 18.37 MB |
| As<T> dispatch | 50,000,000 | 613.23 ms | 81.54 Mops/s | 40 B |

### Blackboard

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| SetValue Int32 × 100k | 100,000 | 10.26 ms | 9.75 Mops/s | 16.70 MB |
| SetValue Single × 100k | 100,000 | 9.95 ms | 10.05 Mops/s | 18.76 MB |
| SetValue String × 100k | 100,000 | 15.61 ms | 6.41 Mops/s | 25.62 MB |
| SetValue Boolean × 100k | 100,000 | 9.15 ms | 10.93 Mops/s | 18.76 MB |
| TryGet Int32 × 500k | 500,000 | 54.89 ms | 9.11 Mops/s | 19.07 MB |
| TryGet Single × 500k | 500,000 | 42.41 ms | 11.79 Mops/s | 19.07 MB |
| TryGet String × 500k | 500,000 | 43.10 ms | 11.60 Mops/s | 19.07 MB |
| TryGet Boolean × 500k | 500,000 | 35.19 ms | 14.21 Mops/s | 19.07 MB |
| Serialize+Deserialize 100 keys | 200 | 40.80 us | 4.90 Mops/s | 4.84 KB |
| Serialize+Deserialize 500 keys | 1,000 | 32.70 us | 30.58 Mops/s | 22.62 KB |
| Serialize+Deserialize 1000 keys | 2,000 | 62.50 us | 32.00 Mops/s | 47.63 KB |

### Save 持久化

| 场景 | 实体数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| ComputePayloadHash 10e | 10 | 59.00 us | 169.49 Kops/s | 53.10 KB |
| ComputePayloadHash 100e | 100 | 253.20 us | 394.94 Kops/s | 503.36 KB |
| ComputePayloadHash 500e | 500 | 1.29 ms | 388.98 Kops/s | 2.31 MB |
| Write+Read 10e | 10 | 129.40 us | 77.28 Kops/s | 27.30 KB |
| Write+Read 100e | 100 | 143.10 us | 698.81 Kops/s | 135.49 KB |
| Write+Read 300e | 300 | 383.10 us | 783.09 Kops/s | 347.25 KB |
| Snapshot Write+Read 10e | 10 | 11.70 us | 854.70 Kops/s | 6.27 KB |
| Snapshot Write+Read 100e | 100 | 7.20 us | 13.89 Mops/s | 6.27 KB |
| Snapshot Write+Read 300e | 300 | 7.40 us | 40.54 Mops/s | 6.27 KB |

### 并发队列

| 场景 | 操作数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Enqueue+ExecuteAll × 100 | 100 | 30.30 us | 3.30 Mops/s | 896 B |
| Enqueue+ExecuteAll × 1,000 | 1,000 | 15.50 us | 64.52 Mops/s | 7.91 KB |
| Enqueue+ExecuteAll × 10,000 | 10,000 | 1.25 ms | 7.98 Mops/s | 78.22 KB |
| Enqueue × 1,000 | 1,000 | 33.50 us | 29.85 Mops/s | 47.53 KB |
| Enqueue × 10,000 | 10,000 | 4.34 ms | 2.31 Mops/s | 568.81 KB |
| Enqueue × 50,000 | 50,000 | 976.30 us | 51.21 Mops/s | 2.53 MB |

### 随机数 (XorShift128+)

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| NextUInt64 | 5,000,000 | 5.43 ms | 920.22 Mops/s | 0 B |
| NextInt64 | 5,000,000 | 5.43 ms | 920.06 Mops/s | 0 B |
| NextInt32 | 5,000,000 | 5.43 ms | 920.01 Mops/s | 0 B |
| Standalone NextUInt64 | 10,000,000 | 10.87 ms | 919.77 Mops/s | 0 B |

### Strategy 性能

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Pool Get+Release × 100k | 100,000 | 105.32 ms | 949.50 Kops/s | 211.52 MB |
| Process 10k frames, 1 strategy | 10,000 | 8.66 ms | 1.15 Mops/s | 96 B |
| Process 10k frames, 5 strategies | 50,000 | 1.49 ms | 33.65 Mops/s | 104 B |
| Process 10k frames, 10 strategies | 100,000 | 2.02 ms | 49.49 Mops/s | 144 B |
| Process 10k frames, 20 strategies | 200,000 | 3.40 ms | 58.80 Mops/s | 224 B |

## Godot 适配器基准

### Vector3 读写与转换

| 场景 | 生成 (Mops/s) | 对照 (Mops/s) | 倍率 | 胜方 | Alloc 生成/对照 |
|------|--------------:|--------------:|:----:|:----:|----------------|
| Read Vector3: TryGet vs ToObject | 445.83 | 102.94 | 4.33x | 生成 | 40 B / 6.10 MB |
| Write Vector3: factory vs fallback | 13.56 | 425.71 | 31.39x | 对照 | 33.57 MB / 6.10 MB |
| FromObject Color: kind-switch vs fallback | 54.88 | 62.50 | 1.14x | 对照 | 12.21 MB / 12.21 MB |

### 单次测量

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| ToObject: Registered Vector3 conversion | 200,000 | 1.90 ms | 105.46 Mops/s | 6.10 MB |
| Create+Extract Vector3 | 400,000 | 16.90 ms | 23.68 Mops/s | 39.67 MB |
| EntitySim 500 entities × 60 frames | 150,000 | 13.12 ms | 11.44 Mops/s | 10.07 MB |

---
[↑ 回到 Origo.manual](../README.zh.md)