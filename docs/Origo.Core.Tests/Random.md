# 随机数 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Random](../Origo.Core/Random/README.md)

## 被测行为概览

验证 XorShift128+ 伪随机数生成器的种子确定性（相同种子 → 相同序列、不同种子 → 不同序列）、
噪声图生成器（OpenSimplex2 + Worley 混合）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `RandomNumberGeneratorTests.cs` | 种子确定性 |
| `RandomNumberGeneratorExtendedTests.cs` | 扩展边缘：边界值、大量生成 |
| `NoiseMapGeneratorTests.cs` | 噪声图生成正确性 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SameSeed_ProducesSameSequence` | "same-seed" 两次生成相同序列 | Random |
| `DifferentSeed_ProducesDifferentSequence` | "seed-a" 和 "seed-b" 序列不同 | Random |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| RNG 连续大量生成不重复 | 大量序列值 | 无崩溃 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| NoiseMapGenerator 在极端尺寸（1x1、10000x10000）时的行为 | 边界尺寸 | Random |

---

[↑ 回到 Origo.Core.Tests](README.md)
