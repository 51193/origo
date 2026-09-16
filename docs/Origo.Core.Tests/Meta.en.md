<!-- docsync-pair: Origo.Core.Tests/Meta -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Framework Meta Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core](../Origo.Core/README.en.md)

## Behavior Overview

Validates the behavior of `OrigoMeta` records: construction of framework identity information (name/version/banner), `ToString` presentation,
default banner constant, and value-based equality semantics.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `OrigoMetaTests.cs` | OrigoMeta default banner, ToString content, value equality/inequality |

## OrigoMetaTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `DefaultBanner_IsNonEmpty` | `OrigoMeta.DefaultBanner` is a non-empty string | Origo.Core |
| `ToString_ContainsNameAndVersion` | `ToString()` contains name and version | Origo.Core |
| `EqualOperator_SameValues_ReturnsTrue` | Two OrigoMeta with same fields are equal, `==` is true | Origo.Core |
| `EqualOperator_DifferentValues_ReturnsFalse` | Two OrigoMeta with different versions are not equal, `==` is false | Origo.Core |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| Constructing OrigoMeta with empty/whitespace name or version not covered | Boundary input not verified | Origo.Core |

---

[↑ Back to Origo.Core.Tests](README.en.md)
