<!-- docsync-pair: benchmarks/baseline -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Origo 性能基线

> [↑ 回到 Origo.manual](../README.zh.md)

> Origo 框架各子系统的性能现状快照，作为后续性能优化的权威对照来源。
> 数值与运行环境、运行时版本强相关，跨机器不可直接比较；优化前后须在**同一环境同一运行时**下复测。

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

- **分配增长超 20%**：任何机器上都判定失败（分配与 CPU 无关，跨机器可比）
- **吞吐下降超 50%**：仅当运行机器与基线记录的 `machine_id` 相同时判定失败（CI runner 是随机机器，吞吐跨机器不可比；本地同机器运行可捕捉真实退化）
- 单次测量的子系统基准受 CPU 调频影响波动可达 ±50%，故阈值按"严重退化检测"设定（复杂度退化、分配泄漏），不追求噪音级灵敏度
- 确认改进或环境变更后，运行 `bash scripts/benchmark.sh --update-baseline` 刷新基线并提交

## 采样元信息

| 项 | 值 |
|----|----|
| CPU | Intel Core i7-11800H（8C/16T，2.30 GHz base） |
| 内存 / OS | 15.4 GiB / Ubuntu 26.04 LTS（Linux 7.0.0-28-generic） |
| .NET | SDK 10.0.302，运行时 10.0.10（测试目标 `net10.0`） |
| 构建 | `Release` |
| 采样 | 单次运行（min-of-rounds 内部取 `min of 5`） |

> 下方数值表格为当前基线快照的参考展示；**回归门禁的数据源是 `docs/benchmarks/baseline.json`**（随机器与运行时变化由 `--update-baseline` 刷新）。

## 方法学

- **时间**：固定容量池 + 位掩码寻址（内存恒定）、大迭代次数（单轮跨多个 OS 时间片）、1 轮 warmup + 多轮取两侧各自最小耗时（剔除被抢占/GC 的离群轮）。
- **分配**：每个基准对每一侧各跑一次专用 `[MethodImpl(NoInlining)]` 测量轮，取 `GC.GetAllocatedBytesForCurrentThread()` 前后差值。测量置于独立方法，使其循环体不与计时循环共用代码生成、不影响吞吐。
- **Alloc 列**：每轮（即表中迭代数对应的一轮）的实测分配，格式「生成 / 装箱」。

> **「Alloc」与相对趋势是最可靠的判据**：跨 4 轮逐字一致。绝对吞吐受 CPU 调频与超平凡循环的代码对齐影响存在抖动（见末尾效度局限）。

## SG 纯净微基准

### 写入吞吐 — 生成 operator vs 装箱 class（2,000,000 迭代）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 | Alloc 生成/装箱 |
|------|--------------:|--------------:|:----:|:----:|----------------|
| Int32   | 226 | 12.3 | 18.5x | 生成 | **0 B / 106.81 MB** |
| Int64   | 229 | 11.6 | 19.7x | 生成 | 0 B / 106.81 MB |
| Single  | 238 | 13.7 | 17.5x | 生成 | 0 B / 106.81 MB |
| Double  | 227 | 12.9 | 17.6x | 生成 | 0 B / 106.81 MB |
| Boolean | 239 | 13.6 | 17.5x | 生成 | 0 B / 106.81 MB |
| Char    | 232 | 12.7 | 18.3x | 生成 | 0 B / 106.81 MB |
| String（ref slot） | 168 | 27.1 | 6.2x | 生成 | 0 B / 61.04 MB |

### 读取吞吐 — 生成 TryGet/Kind vs 装箱 `is T`（10,000,000 迭代，两侧 Alloc 均 0 B）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 | 稳定性 |
|------|--------------:|--------------:|:----:|:----:|--------|
| Int32   | 248 | 185 | 1.34x | 生成 | 稳 |
| Int64   | 231 | 216 | 1.07x | 生成 | 稳 |
| Single  | 254 | 189 | 1.35x | 生成 | 稳 |
| Double  | 257 | 214 | 1.20x | 生成 | 稳 |
| Boolean | 249 | 200 | 1.24x | 生成 | 稳 |
| Char    | 256 | 225 | 1.14x | 生成 | 稳 |
| String（`TryGetString`） | 191 | 313 | ~1.64x | 装箱 | 高方差 |
| String（`IsString`） | 313 | 201 | 1.56x | 生成 | 稳 |

### 混合分派 — 生成 Kind switch vs 装箱 `is T`（10,000,000 迭代，两侧 0 B）

| 场景 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| 混合分派（int/float/bool/string/double） | 366 | 116 | ~3.15x | 生成 |

## Core 真实模拟基准

### 异构字典迭代（2,048,000 次 `.Data` 读取）

| 场景 | 生成 .Data (Mops/s) | 装箱迭代 (Mops/s) | 倍率 | 胜方 | Alloc 生成/装箱 |
|------|--------------------:|------------------:|:----:|:----:|----------------|
| 异构字典 `.Data` 迭代 | 74 | 520 | ~7.0x | 装箱 | 37.49 MB / 0 B |

> 这是**合成最坏情况**：`.Data` 返回 `object`，值类型每次读取经 `ToObject` 重新装箱（数据集 ~80% 为值类型 → 每轮 37.49 MB）。生产无此调用形态（见「设计权衡」）。

### 工厂构造 + 字典插入（500,000 迭代）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 | Alloc 生成/装箱 |
|------|--------------:|--------------:|:----:|:----:|----------------|
| String  | 49.1 | 54.0 | 1.10x | 装箱 | 23.53 MB / 14.97 MB |
| Int32   | 50.1 | 15.2 | 3.30x | 生成 | 23.53 MB / 26.42 MB |
| Single  | 60.0 | 18.6 | 3.24x | 生成 | 23.53 MB / 26.42 MB |
| Boolean | 53.1 | 18.7 | 2.84x | 生成 | 23.53 MB / 26.42 MB |

> 值类型插入装箱侧多出 ~12 MB 装箱（26.42 vs 23.53），生成更快且更省。String 插入生成侧反而略多分配（23.53 vs 14.97 MB）：`Dictionary<string,TypedData>` 每 entry 内嵌 24 字节结构体，后备数组大于 `Dictionary<string,object>` 的 8 字节引用。

### 观察者通知（2,000,000 迭代，两侧 0 B）

| 场景 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| Observer notify (old,new) + 判型 | 386 | 361 | ~1.07x | 生成 |

> 经 `TypedData` 传递（非 `object`），用 `TryGetString` 判型，零装箱，与装箱 `is string` 持平。

### 字典查找 TryExtract（2,000,000 迭代，两侧 0 B）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| String  | 55.5 | 44.5 | 1.25x | 生成 |
| Int32   | 39.2 | 39.4 | 1.00x | 装箱 |
| Single  | 42.1 | 34.9 | 1.21x | 生成 |
| Boolean | 41.4 | 31.5 | 1.32x | 生成 |

### 多类型强转链 float→int→long→double（2,000,000 迭代，int payload，两侧 0 B）

| 场景 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| 数值强转链 | 102 | 85.9 | 1.19x | 生成 |

## 性能现状概览

- **值类型**：写入 ~17–20x、混合分派 ~3.15x、字典构造+插入 ~3x 远超装箱；单读 1.07–1.35x（全部超越装箱）。**目标达成。**
- **string**：写入 6.2x、DictLookup 1.25x、IsString 1.56x 超装箱；`TryGetString` 数组读取 ~1.64x 慢（结构性，见下）。
- **DictLookup 值类型**：Int32 近乎持平（1.00x），Single/Boolean 生成更快（1.21–1.32x）。与上一轮基线（Ryzen 7 9700X）相比，低端 CPU 上生成代码的相对优势更加明显，因为装箱路径的 GC 写屏障开销在弱缓存/低主频下相对更重。
- **观察者通知**：生成 1.07x 优于装箱。
- 唯一「装箱更快」项——`TryGetString` 数组读取——为结构性（见下），且 `IsString` 路径生成 1.56x 超装箱。

## 设计权衡与已评估方向

记录现状为何如此、做了哪些取舍，以及**已验证无效、不应重试**的方向，避免后续迭代走老路。

- **`TryGetString` 用 `Unsafe.As<string>`（受 `_kind == String` 守卫）而非 `(string)_ref`**：守卫已保证 `_ref` 是 string，`castclass` 是冗余的；且 `castclass` 可抛异常，会阻断 JIT 对「结果被丢弃/循环不变」的 `TryGetString` 调用的消除与外提。在**操作绑定**的 `TryGetString` 路径（如观察者通知）上，去掉它使生成侧与装箱 `is string` 持平。

- **【已验证无效，勿重试】去 `castclass` 对「数组 string 读取」无可测收益**：同一改动在缓存绑定的数组读取上仅带来噪声内的变化（中位约 +2%），不改变 ~1.40x 的结论。该路径瓶颈是**缓存而非指令**——`_ref` 在结构体偏移 16，在 24 字节步长 + 64 字节缓存行下比值类型读取的 `_inlineBits`（偏移 8）更易跨缓存行。指令级微调无法改善，勿在此方向反复尝试。

- **【已评估不划算，勿尝试】把 `TypedData` 结构体压到 16 字节**：当前 24 字节（`byte _kind` + `long _inlineBits` + `object? _ref`）是 GC 安全设计的下限——`long` 与托管引用槽不能重叠（GC 需独立扫描引用），`_kind` 字节无空闲位可塞。压到 16 字节须牺牲 `long`/`double` 的满 64 位内联，或引入额外分支/类型查找（多半负收益），且改动 `internal` 布局（被生成代码与测试经 `InternalsVisibleTo` 依赖）。值类型单读 ≤1.10x、DictLookup 值类型 1.31–1.38x 的结构性差距即源于此，已接受。

- **`.Data`（object?）的装箱只落在冷路径**：`.Data` 经 `ToObject` 对值类型装箱，服务于编译期无法得知类型的冷路径（按 `DataType` 的序列化、控制台、`ToString`），这些路径固有需要 `object`。框架内热/温路径（数据变更信号处理、加载校验等）一律用零装箱的 `TryGetXxx`。**「异构 `.Data` 迭代」基准（~6.9x、37.49 MB）是合成最坏情况，不对应任何真实生产热路径**；移除 `.Data` 只会把同样的装箱挪进内部并增加复杂度（下游零依赖、~60 处测试依赖其便利访问）。取舍与推荐用法见 [Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.zh.md)。

## 效度局限

1. **CPU 动态调频（scaling ≈ 75%）** 引入运行间抖动；针对 ≤ 1.3x 的边际项与最快循环（异构迭代装箱侧 6.3–8.4x、写入），建议固定频率/性能模式后复测。
2. **绝对吞吐受代码对齐影响**：写入等超平凡循环对方法布局/对齐敏感，存在 ~±8% 抖动；**分配量与相对趋势不受此影响**，应作为主要判据。
3. **运行时为 .NET 10.0.9**，与测试目标 `net10.0` 一致；结论须在同一运行时下复测。
4. **微基准取 min-of-rounds**，偏向理想 JIT 稳态；真实模拟套件更具代表性。
5. 高方差项：String `TryGetString` 读取、混合分派、异构迭代装箱侧、值类型插入装箱侧；对该些项下结论前应增加采样轮数（≥ 8 轮）收敛。

## 子系统性能基线

> 以下为框架核心子系统的性能快照，按模块分组。每种基准取 min-of-rounds 或单次测量。

### 实体生命周期

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| 100 entities create+spawn | 100 | 815.40 us | 122.64 Kops/s | 392.72 KB |
| 500 entities create+spawn | 500 | 2.43 ms | 205.47 Kops/s | 1.91 MB |
| 2000 entities create+spawn | 2,000 | 22.41 ms | 89.26 Kops/s | 7.64 MB |

### 帧处理

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| 10 entities × 1 strategy, 200 frames | 2,000 | 360.40 us | 5.55 Mops/s | 600 B |
| 50 entities × 5 strategies, 200 frames | 50,000 | 4.18 ms | 11.97 Mops/s | 3.16 KB |
| 200 entities × 10 strategies, 200 frames | 400,000 | 20.56 ms | 19.45 Mops/s | 20.35 KB |

### 实体存档 (SaveSingle)

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| 10 entities SaveSingle | 10 | 167.40 us | 59.74 Kops/s | 9.96 KB |
| 100 entities SaveSingle | 100 | 173.20 us | 577.37 Kops/s | 99.26 KB |
| 500 entities SaveSingle | 500 | 1.39 ms | 360.65 Kops/s | 496.13 KB |

### Observer 拓扑

#### Mount

| 场景 | 操作数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Mount × 10 | 10 | 198.60 us | 50.35 Kops/s | 22.31 KB |
| Mount × 50 | 50 | 153.00 us | 326.80 Kops/s | 105.89 KB |
| Mount × 200 | 200 | 705.10 us | 283.65 Kops/s | 424.18 KB |

#### Unmount

| 场景 | 操作数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Unmount × 10 | 10 | 217.40 us | 46.00 Kops/s | 18.50 KB |
| Unmount × 50 | 50 | 405.30 us | 123.37 Kops/s | 87.69 KB |
| Unmount × 200 | 200 | 1.97 ms | 101.30 Kops/s | 348.11 KB |

### DataSourceNode 树

| 场景 | 节点数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Tree build d=2 w=5 | ~31 | 186.50 us | 166.22 Kops/s | 40.16 KB |
| Tree build d=3 w=8 | ~585 | 1.79 ms | 327.09 Kops/s | 763.25 KB |
| Tree build d=4 w=8 | ~4,681 | 21.98 ms | 212.98 Kops/s | 6.07 MB |
| SHA-256 hash d=3 w=8 | ~585 | 8.14 ms | 71.85 Kops/s | 1.58 MB |
| SHA-256 hash d=4 w=8 | ~4,681 | 45.74 ms | 102.35 Kops/s | 16.04 MB |
| As\<T\> dispatch | 50,000,000 | 2.30 s | 21.73 Mops/s | 40 B |

### Blackboard

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| SetValue Int32 × 100k | 100,000 | 47.77 ms | 2.09 Mops/s | 16.70 MB |
| SetValue Single × 100k | 100,000 | 103.39 ms | 967.17 Kops/s | 18.76 MB |
| SetValue String × 100k | 100,000 | 171.10 ms | 584.44 Kops/s | 25.62 MB |
| SetValue Boolean × 100k | 100,000 | 73.20 ms | 1.37 Mops/s | 18.76 MB |
| TryGet Int32 × 500k | 500,000 | 115.50 ms | 4.33 Mops/s | 19.07 MB |
| TryGet Single × 500k | 500,000 | 120.19 ms | 4.16 Mops/s | 19.07 MB |
| TryGet String × 500k | 500,000 | 111.47 ms | 4.49 Mops/s | 19.07 MB |
| TryGet Boolean × 500k | 500,000 | 114.01 ms | 4.39 Mops/s | 19.07 MB |
| Serialize+Deserialize 100 keys | 200 | 145.90 us | 1.37 Mops/s | 4.84 KB |
| Serialize+Deserialize 500 keys | 1,000 | 104.70 us | 9.55 Mops/s | 22.62 KB |
| Serialize+Deserialize 1000 keys | 2,000 | 351.00 us | 5.70 Mops/s | 47.63 KB |

### Save 持久化

| 场景 | 实体数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| ComputePayloadHash 10e | 10 | 332.20 us | 30.10 Kops/s | 40.76 KB |
| ComputePayloadHash 100e | 100 | 2.30 ms | 43.42 Kops/s | 350.30 KB |
| ComputePayloadHash 500e | 500 | 5.43 ms | 92.07 Kops/s | 1.67 MB |
| Write+Read 10e | 10 | 316.10 us | 31.64 Kops/s | 25.42 KB |
| Write+Read 100e | 100 | 489.10 us | 204.46 Kops/s | 129.40 KB |
| Write+Read 300e | 300 | 1.70 ms | 176.13 Kops/s | 331.78 KB |
| Snapshot Write+Read 10e | 10 | 121.80 us | 82.10 Kops/s | 6.30 KB |
| Snapshot Write+Read 100e | 100 | 145.20 us | 688.71 Kops/s | 6.30 KB |
| Snapshot Write+Read 300e | 300 | 77.50 us | 3.87 Mops/s | 6.30 KB |

### 并发队列

| 场景 | 操作数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Enqueue+ExecuteAll × 100 | 100 | 75.10 us | 1.33 Mops/s | 896 B |
| Enqueue+ExecuteAll × 1,000 | 1,000 | 86.70 us | 11.53 Mops/s | 7.91 KB |
| Enqueue+ExecuteAll × 10,000 | 10,000 | 2.32 ms | 4.31 Mops/s | 78.22 KB |
| Enqueue × 1,000 | 1,000 | 137.90 us | 7.25 Mops/s | 16.28 KB |
| Enqueue × 10,000 | 10,000 | 12.31 ms | 812.66 Kops/s | 256.31 KB |
| Enqueue × 50,000 | 50,000 | 3.13 ms | 15.98 Mops/s | 1.00 MB |

### 随机数 (XorShift128+)

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| NextUInt64 | 10,000,000 | 19.62 ms | 509.63 Mops/s | 0 B |
| NextInt64 | 5,000,000 | 9.37 ms | 533.53 Mops/s | 0 B |
| NextInt32 | 5,000,000 | 9.26 ms | 539.95 Mops/s | 0 B |

### Strategy 性能

| 场景 | 迭代数 | 耗时 | 吞吐 | 分配 |
|------|--------|------|------|------|
| Pool Get+Release × 100k | 100,000 | 455.76 ms | 219.41 Kops/s | 219.15 MB |
| Process 10k frames, 1 strategy | 10,000 | 22.35 ms | 447.43 Kops/s | 96 B |
| Process 10k frames, 5 strategies | 50,000 | 820.30 us | 60.95 Mops/s | 104 B |
| Process 10k frames, 10 strategies | 100,000 | 1.01 ms | 98.89 Mops/s | 144 B |
| Process 10k frames, 20 strategies | 200,000 | 1.70 ms | 117.88 Mops/s | 224 B |
