<!-- docsync-pair: Origo.Core/Addons/FastNoiseLite/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# FastNoiseLite

> [↑ Back to Addons](../README.en.md)

## Overview

Third-party noise library **FastNoiseLite v1.1.1**, developed by Jordan Peck, MIT license. Provides multiple noise types including OpenSimplex2, Cellular (Worley), Perlin, Value, as well as Domain Warp functionality. This project uses it as a vendor import only; the source code has not been modified.

## Included Files

| File | Responsibility |
|------|---------------|
| `FastNoiseLite.cs` | Complete noise library implementation (~2700 lines), all algorithms in a single file |

## Capabilities

| Feature | Description |
|---------|-------------|
| `OpenSimplex2` / `OpenSimplex2S` | Modern smooth noise |
| `Cellular` | Worley noise, supports multiple distance functions and return types |
| `Perlin` / `Value` / `ValueCubic` | Classic noise types |
| `DomainWarp` | Domain warping, supports multiple warp types |
| `Fractal` | FBm, Ridged, PingPong fractal layering |
| `SetSeed` / `SetFrequency` | Seed and frequency control |

## Design Decisions

### Why vendor rather than NuGet package

FastNoiseLite is a single-file implementation with zero external dependencies. The vendor approach avoids additional package management burden, and the library is stable (no updates needed since v1.1.1). See `THIRD_PARTY_NOTICES.md` in the `origo` repo root.

### Why not modify the source code

Preserves upstream traceability. If upstream updates in the future, simply replace the file. All adaptation (e.g., noise map generation) is done in the outer-layer `NoiseMapGenerator` (see [Random](../../Random/README.en.md)).

### Why use `float` as the base numeric type

Controlled via the `using FNLfloat = float;` alias. float precision is sufficient for game scenarios, and in Godot and most game engines, float is the native coordinate/numeric type, avoiding double-precision conversion overhead.

---
[↑ Back to Addons](../README.en.md)
