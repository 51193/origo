# FastNoiseLite

> [↑ 回到 Addons](../README.md)

## 概述

第三方噪声库 **FastNoiseLite v1.1.1**，由 Jordan Peck 开发，MIT 许可。提供 OpenSimplex2、Cellular (Worley)、Perlin、Value 等多种噪声类型，以及域扭曲（Domain Warp）功能。本项目仅作 vendor 引入，未修改源码。

## 包含文件

| 文件 | 职责 |
|------|------|
| `FastNoiseLite.cs` | 完整噪声库实现（~2700 行），所有算法在一份文件中 |

## 能力

| 功能 | 说明 |
|------|------|
| `OpenSimplex2` / `OpenSimplex2S` | 现代平滑噪声 |
| `Cellular` | Worley 噪声，支持多种距离函数和返回值类型 |
| `Perlin` / `Value` / `ValueCubic` | 经典噪声类型 |
| `DomainWarp` | 域扭曲，支持多种扭曲类型 |
| `Fractal` | FBm、Ridged、PingPong 分形叠加 |
| `SetSeed` / `SetFrequency` | 种子和频率控制 |

## 设计决策

### 为什么采用 vendor 而非 NuGet 包

FastNoiseLite 为单文件实现，无任何外部依赖。vendor 方式避免了额外的包管理负担，且该库稳定（v1.1.1 后无更新需求）。详见 `origo` 仓库根目录的 `THIRD_PARTY_NOTICES.md`。

### 为什么不修改源码

保持上游可追踪性。如果未来上游有更新，直接替换文件即可。所有适配（如噪声图生成）在外层 `NoiseMapGenerator`（见 [Random](../../Random/README.md)）中完成。

### 为什么使用 `float` 作为基础数值类型

通过 `using FNLfloat = float;` 别名控制。游戏场景中 float 精度足够，且在 Godot 和大多数游戏引擎中 float 是原生坐标/数值类型，避免双精度转换开销。

---
[↑ 回到 Addons](../README.md)
