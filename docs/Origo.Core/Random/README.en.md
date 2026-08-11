<!-- docsync-pair: Origo.Core/Random/README -->
<!-- docsync-revision: 3 -->
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
- **TryNextInt32**: Sequential read → advance → write (not atomic; single-threaded frame model only, a generator must not be shared across threads). Returns false when uninitialized
- The constructor accepts optional custom state key names (default `"rand.state1"`, `"rand.state2"`). **The default keys are shared constants**: two generators on the same blackboard with default keys silently share (and overwrite) each other's state — pass distinct custom keys unless sharing is intended

## Design Decisions

### Why random state is explicitly maintained
Global state is hard to trace across multi-entity, multi-session, parallel-save scenarios. Explicit state per entity/session enables save serialization and consistent sequences.

> **Consumption note**: the Random module has no production consumer inside the Core repository (test-only usage) — it is provided as a framework capability for game-side consumption (e.g. origo.demo).

### Why XorShift128+ instead of System.Random
`System.Random` lacks cross-version consistency and serializable state. XorShift128+ has 16-byte state and cross-platform consistency.

### Why noise ratio 70/30
Simplex provides macro terrain structure; Worley provides micro detail. Ratio chosen through experimentation.

### Why no 3D noise
2D noise covers current game needs (terrain, heightmaps). 3D noise has a different API (z parameter) and is 1-2 orders of magnitude more expensive. It is not introduced early without a concrete requirement.

---
[↑ Back to Origo.Core](../README.en.md)
