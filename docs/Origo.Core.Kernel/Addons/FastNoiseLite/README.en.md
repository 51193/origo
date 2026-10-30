# FastNoiseLite

> [↑ Back to Addons](../README.en.md)

## Overview

Third-party noise library **FastNoiseLite v1.1.1**, developed by Jordan Peck, MIT license. Provides multiple noise types including OpenSimplex2, Cellular (Worley), Perlin, Value, as well as Domain Warp functionality. This project vendors the upstream C# source with mechanical adaptations only; the algorithm and the public `FastNoiseLite` API are unchanged (see [Upstream adaptation](#upstream-adaptation)).

## Included Files

| File | Responsibility |
|------|---------------|
| `FastNoiseLite.cs` | Complete noise library implementation; all algorithms live in a single file |

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

### Why keep the algorithm unmodified

The upstream algorithm stays behaviorally identical, so upstream traceability, benchmark comparability, and MIT attribution remain intact. Feature logic (e.g., noise map generation) lives in the outer-layer `NoiseMapGenerator` (see [Random](../../../Origo.Core/Random/README.en.md)) instead of the vendored file.

## Upstream adaptation

The vendored `FastNoiseLite.cs` differs from upstream v1.1.1 only by mechanical adaptations required to live in this repository:

- file-scoped `namespace Origo.Core.Addons.FastNoiseLite`;
- private fields/constants renamed to the repository's `_camelCase` convention (enforced by the private-field architecture guard);
- formatter/analyzer rewrites (`dotnet format` style, enum ordering, `var` usage, XML-comment formatting) so the file passes the repository format gate.

The noise algorithms, constants, public API, and numeric behavior are unchanged. To re-vendor an upstream version: replace the file with the upstream source, re-apply the namespace/private-field naming, run `bash scripts/bash/format.sh` plus the noise tests and benchmark, and update the version in this document and `THIRD_PARTY_NOTICES.md` in the same change.

### Why use `float` as the base numeric type

Controlled via the `using FNLfloat = float;` alias. float precision is sufficient for game scenarios, and in Godot and most game engines, float is the native coordinate/numeric type, avoiding double-precision conversion overhead.

---
[↑ Back to Addons](../README.en.md)
