<!-- docsync-pair: Origo.Core.Tests/Random -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Random Number Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Random](../Origo.Core/Random/README.en.md)

## Behavior Overview

Validates the XorShift128+ pseudo-random number generator (seed determinism, NextUInt64/NextInt32/NextInt64, state continuity),
PersistentRandom (blackboard-backed persistent random number generator, including init/range constraints/boundary guards),
and noise map generator (OpenSimplex2 + Worley blend).

## Test File List

| File | Verification Focus |
|------|-------------------|
| `RandomNumberGeneratorTests.cs` | Seed determinism (same seed→same sequence, different seed→different sequence) |
| `RandomNumberGeneratorExtendedTests.cs` | Extended edge cases: NextInt32/NextInt64 return types, uniqueness of large batches, consistent output from same state |
| `NoiseMapGeneratorTests.cs` | Noise map generation correctness: size/range/seed determinism/parameter differences/invalid size |
| `PersistentRandomTests.cs` | Persistent random number generator: blackboard init, state storage, sequence determinism, range constraints, uninitialized guard |
| `RandomAndStateMachineTests.Random.cs` | RNG state consistency: NextInt32/NextInt64 stepping consistent with NextUInt64, state chain continuity |

## RandomNumberGeneratorTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SameSeed_ProducesSameSequence` | "same-seed" generates identical sequence twice | Random |
| `DifferentSeed_ProducesDifferentSequence` | "seed-a" and "seed-b" produce different sequences | Random |

## RandomNumberGeneratorExtendedTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `RandomNumberGenerator_SameSeed_ProducesSameSequence` | Same seed produces same sequence | Random |
| `RandomNumberGenerator_DifferentSeed_ProducesDifferentSequence` | Different seed produces different sequence | Random |
| `RandomNumberGenerator_NextInt32_ReturnsValue` | NextInt32 returns int value, does not throw | Random |
| `RandomNumberGenerator_NextInt64_ReturnsValue` | NextInt64 returns long value, does not throw | Random |
| `RandomNumberGenerator_Sequence_IsNotConstant` | Unique values out of 100 consecutive > 90 | Random |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `RandomNumberGenerator_SameState_SameFirstValue` | Same (s0, s1) calls NextUInt64 twice | Returns same first value |

## NoiseMapGeneratorTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `GenerateSimplexWorleyBlendMap_ReturnsExpectedLengthAndRange` | size=32 map length is 32×32, all values ∈ [0,1] | Random/NoiseMapGenerator |
| `GenerateSimplexWorleyBlendMap_SameSeed_ProducesSameResult` | Same seed produces completely identical map | Random/NoiseMapGenerator |
| `ExtendedOverload_ProducesValidRange` | Overload with all parameters returns valid range | Random/NoiseMapGenerator |
| `ExtendedOverload_WithDifferentOctaves_ProducesDifferentResult` | Different octaves parameter produces different result | Random/NoiseMapGenerator |
| `ExtendedOverload_SameParams_SameResult` | Completely identical parameters produce identical result | Random/NoiseMapGenerator |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `GenerateSimplexWorleyBlendMap_InvalidSize_Throws` | size=0 or size=-4 | ArgumentOutOfRangeException, ParamName="size" |

## PersistentRandomTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `InitSeed_StoresStateInBlackboard` | InitSeed stores RNG state in blackboard | Random/PersistentRandom |
| `InitSeed_ThenNextInt32_ReturnsTrue` | After init, TryNextInt32 returns true | Random/PersistentRandom |
| `SameSeed_ProducesSameSequence` | Same seed after init produces identical sequence | Random/PersistentRandom |
| `DifferentSeed_ProducesDifferentSequence` | Different seed produces different sequence | Random/PersistentRandom |
| `NextInt32_Ranged_WithinBounds` | NextInt32(min, max) returns within [min, max) for 100 calls | Random/PersistentRandom |
| `NextFloat_InRange` | NextFloat returns within [0, 1) for 100 calls | Random/PersistentRandom |
| `NextFloat_IsStrictlyLessThanOne` | 10000 NextFloat calls all satisfy 0 ≤ value < 1.0 | Random/PersistentRandom |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `NextInt32_MaxEqualsMin_Throws` | max=min | ArgumentOutOfRangeException |
| `NextInt32_MaxLessThanMin_Throws` | max < min | ArgumentOutOfRangeException |
| `NextInt32_BeforeInit_Throws` | Call NextInt32 before InitSeed | InvalidOperationException |
| `NextFloat_BeforeInit_Throws` | Call NextFloat before InitSeed | InvalidOperationException |
| `NullBlackboard_Throws` | null blackboard parameter | ArgumentNullException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `TryNextInt32_BeforeInit_ReturnsFalse` | Call TryNextInt32 when uninitialized | Returns false |
| `CustomStateKeys_UseProvidedKeys` | Custom blackboard key names | Uses provided keys, init works normally |
| `NextInt32_LargeSpan_StaysWithinBounds` | Span wider than int.MaxValue (e.g. [-5, int.MaxValue)) | 2000 draws all within [min, max) (uint range math does not overflow) |
| `NextFloat_EdgeRawValuesThatRoundToOne_AreClamped` | Raw state values in [2^32-2^7, 2^32) that would round to 1.0f | NextFloat clamps result below 1.0f |

## RandomAndStateMachineTests.Random Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `CreateStateFromSeed_SameSeed_ProducesSameState` | Same seed creates same (s0, s1) state | Random |
| `DifferentSeed_ProducesDifferentSequence` | Different seed produces different sequence | Random |
| `SameState_ProducesSameSequence` | Same initial state twice produces same sequence | Random |
| `ReturnedState_CanContinueSequence` | State returned by NextUInt64 can continue the sequence | Random |
| `NextInt32AndNextInt64_StayConsistentWithNextUInt64Step` | NextInt32/NextInt64 stepping consistent with NextUInt64, type conversion correct | Random |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| None | — | All tests are pure static function calls, no helper strategies needed |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| NoiseMapGenerator behavior at extreme sizes (1×1, 10000×10000) | Boundary sizes | Random/NoiseMapGenerator |
| PersistentRandom state behavior after multiple InitSeed (re-seeding) | Re-seeding semantics | Random/PersistentRandom |

---

[↑ Back to Origo.Core.Tests](README.en.md)
