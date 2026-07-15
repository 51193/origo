<!-- docsync-pair: Origo.Core/Random/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Random

> [↑ Back to Origo.Core](../README.en.md) · [↔ Addons: FastNoiseLite](../Addons/FastNoiseLite/README.en.md)

## Overview
Unified entry point for game random numbers. Two independent capabilities: XorShift128+ pseudo-random number generation and 2D noise map generation via FastNoiseLite. Both are reproducible given the same seed.

## Included Files

| File | Responsibility |
|------|------|
| `RandomNumberGenerator.cs` | XorShift128+ PRNG, state explicitly maintained by caller |
| `PersistentRandom.cs` | Persists random state to progress blackboard |
| `NoiseMapGenerator.cs` | Simplex + Worley blended noise map generation |

## Implementation Details

### RandomNumberGenerator
- **Algorithm**: XorShift128+ (period 2^128-1)
- **State model**: Returns `(value, nextS0, nextS1)` tuple; caller holds state
- **Seed**: String → FNV-1a 64-bit hash → state pair

### NoiseMapGenerator
- **Algorithm**: OpenSimplex2 (70%) + Worley Cellular (30%)
- **Output**: Row-major `float[size*size]` array, range `[0, 1]`
- **Extended overload**: Support for octaves, lacunarity, gain

### PersistentRandom
- Wraps `IBlackboard` storing state as two `ulong` key-value pairs
- **InitSeed(seed)**: String seed → FNV-1a hash → XorShift128+ state → write to blackboard
- **TryNextInt32**: Atomic read → advance → write. Returns false when uninitialized

## Design Decisions

### Why random state is explicitly maintained
Global state is hard to trace across multi-entity, multi-session, parallel-save scenarios. Explicit state per entity/session enables save serialization and consistent sequences.

### Why XorShift128+ instead of System.Random
`System.Random` lacks cross-version consistency and serializable state. XorShift128+ has 16-byte state and cross-platform consistency.

### Why noise ratio 70/30
Simplex provides macro terrain structure; Worley provides micro detail. Ratio chosen through experimentation.

---
[↑ Back to Origo.Core](../README.en.md)
