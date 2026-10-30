# Addons

> [↑ Back to Origo.Core.Kernel](../README.en.md)

## Module Capability

Collection of vendor (source-imported) third-party libraries. Currently contains only the FastNoiseLite noise library. Vendor files keep the upstream algorithm and public API unchanged; each vendored file records the mechanical adaptations required by this repository's gates (namespace, private-field naming, formatter/analyzer style). Feature logic is added in outer-layer modules, never inside the vendored file.

## Sub-Modules

| Sub-Module | Source | License | Details |
|-----------|--------|---------|---------|
| [FastNoiseLite](FastNoiseLite/README.en.md) | Jordan Peck / v1.1.1 | MIT | Multi-type noise generation (OpenSimplex2/Cellular/Perlin/DomainWarp) |

## This Layer's Files

This directory contains only sub-directories; there are no direct `.cs` files.

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
