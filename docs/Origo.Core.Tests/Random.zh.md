<!-- docsync-pair: Origo.Core.Tests/Random -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 随机数 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Random](../Origo.Core/Random/README.zh.md)

## 被测行为概览

验证 XorShift128+ 伪随机数生成器（种子确定性、NextUInt64/NextInt32/NextInt64、状态连续性）、
PersistentRandom（黑板承载的持久随机数生成器，含初始化/范围约束/边界防护）、
噪声图生成器（OpenSimplex2 + Worley 混合）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `RandomNumberGeneratorTests.cs` | 种子确定性（相同种子→相同序列，不同种子→不同序列） |
| `RandomNumberGeneratorExtendedTests.cs` | 扩展边缘：NextInt32/NextInt64 返回类型、大量生成的唯一性、同状态输出一致 |
| `NoiseMapGeneratorTests.cs` | 噪声图生成正确性：尺寸/范围/种子确定性/参数差异/无效尺寸 |
| `PersistentRandomTests.cs` | 持久随机数生成器：黑板初始化、状态存储、序列确定性、范围约束、未初始化防护 |
| `RandomAndStateMachineTests.Random.cs` | RNG 状态一致性：NextInt32/NextInt64 与 NextUInt64 步进一致、状态链连续 |

## RandomNumberGeneratorTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SameSeed_ProducesSameSequence` | "same-seed" 两次生成相同序列 | Random |
| `DifferentSeed_ProducesDifferentSequence` | "seed-a" 和 "seed-b" 序列不同 | Random |

## RandomNumberGeneratorExtendedTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RandomNumberGenerator_SameSeed_ProducesSameSequence` | 相同种子产生相同序列 | Random |
| `RandomNumberGenerator_DifferentSeed_ProducesDifferentSequence` | 不同种子产生不同序列 | Random |
| `RandomNumberGenerator_NextInt32_ReturnsValue` | NextInt32 返回 int 类型值，不抛异常 | Random |
| `RandomNumberGenerator_NextInt64_ReturnsValue` | NextInt64 返回 long 类型值，不抛异常 | Random |
| `RandomNumberGenerator_Sequence_IsNotConstant` | 100 个连续值中唯一值 > 90 | Random |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `RandomNumberGenerator_SameState_SameFirstValue` | 同一 (s0, s1) 两次调用 NextUInt64 | 返回相同首个值 |

## NoiseMapGeneratorTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GenerateSimplexWorleyBlendMap_ReturnsExpectedLengthAndRange` | size=32 时 map 长度为 32×32，所有值 ∈ [0,1] | Random/NoiseMapGenerator |
| `GenerateSimplexWorleyBlendMap_SameSeed_ProducesSameResult` | 相同 seed 产生完全一致的 map | Random/NoiseMapGenerator |
| `ExtendedOverload_ProducesValidRange` | 带全部参数的重载返回合法范围 | Random/NoiseMapGenerator |
| `ExtendedOverload_WithDifferentOctaves_ProducesDifferentResult` | 不同 octaves 参数产生不同结果 | Random/NoiseMapGenerator |
| `ExtendedOverload_SameParams_SameResult` | 完全相同参数产生相同结果 | Random/NoiseMapGenerator |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `GenerateSimplexWorleyBlendMap_InvalidSize_Throws` | size=0 或 size=-4 | ArgumentOutOfRangeException，ParamName="size" |

## PersistentRandomTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `InitSeed_StoresStateInBlackboard` | InitSeed 将 RNG 状态存入黑板 | Random/PersistentRandom |
| `InitSeed_ThenNextInt32_ReturnsTrue` | 初始化后 TryNextInt32 返回 true | Random/PersistentRandom |
| `SameSeed_ProducesSameSequence` | 相同种子初始化后序列完全一致 | Random/PersistentRandom |
| `DifferentSeed_ProducesDifferentSequence` | 不同种子产生不同序列 | Random/PersistentRandom |
| `NextInt32_Ranged_WithinBounds` | NextInt32(min, max) 返回 [min, max) 内 100 次 | Random/PersistentRandom |
| `NextFloat_InRange` | NextFloat 返回 [0, 1) 内 100 次 | Random/PersistentRandom |
| `NextFloat_IsStrictlyLessThanOne` | 10000 次 NextFloat 均满足 0 ≤ 值 < 1.0 | Random/PersistentRandom |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `NextInt32_MaxEqualsMin_Throws` | max=min | ArgumentOutOfRangeException |
| `NextInt32_MaxLessThanMin_Throws` | max < min | ArgumentOutOfRangeException |
| `NextInt32_BeforeInit_Throws` | InitSeed 前调用 NextInt32 | InvalidOperationException |
| `NextFloat_BeforeInit_Throws` | InitSeed 前调用 NextFloat | InvalidOperationException |
| `NullBlackboard_Throws` | null 黑板参数 | ArgumentNullException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TryNextInt32_BeforeInit_ReturnsFalse` | 未初始化时调用 TryNextInt32 | 返回 false |
| `CustomStateKeys_UseProvidedKeys` | 自定义黑板键名 | 使用提供的键名，初始化正常工作 |
| `NextInt32_LargeSpan_StaysWithinBounds` | 跨度超过 int.MaxValue 的区间（如 [-5, int.MaxValue)） | 2000 次取值均落在 [min, max) 内（uint 范围数学不溢出） |
| `NextFloat_EdgeRawValuesThatRoundToOne_AreClamped` | 原始值落在 [2^32-2^7, 2^32) 会舍入为 1.0f 的边界状态 | NextFloat 钳制结果 < 1.0f |

## RandomAndStateMachineTests.Random 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateStateFromSeed_SameSeed_ProducesSameState` | 相同种子创建相同的 (s0, s1) 状态 | Random |
| `DifferentSeed_ProducesDifferentSequence` | 不同种子产生不同序列 | Random |
| `SameState_ProducesSameSequence` | 相同初始状态两次产生相同序列 | Random |
| `ReturnedState_CanContinueSequence` | NextUInt64 返回的状态可连续产生序列 | Random |
| `NextInt32AndNextInt64_StayConsistentWithNextUInt64Step` | NextInt32/NextInt64 与 NextUInt64 步进一致，类型转换正确 | Random |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 所有测试为纯静态函数调用，不需要辅助策略 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| NoiseMapGenerator 在极端尺寸（1×1、10000×10000）时的行为 | 边界尺寸 | Random/NoiseMapGenerator |
| PersistentRandom 多次 InitSeed（重新播种）后的状态行为 | 重新播种语义 | Random/PersistentRandom |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
