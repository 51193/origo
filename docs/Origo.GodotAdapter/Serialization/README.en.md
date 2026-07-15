<!-- docsync-pair: Origo.GodotAdapter/Serialization/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Serialization

> [↑ Back to Origo.GodotAdapter](../README.en.md) · [↔ Core: Serialization](../../Origo.Core/Serialization/README.en.md)

## Overview

Registration of Godot engine types in the Origo serialization system. Adds support for 14 Godot built-in types to Core's `TypeStringMapping` and `DataSourceConverterRegistry`, enabling engine types like `Vector2`, `Vector3`, `Color`, `Transform3D`, etc. to be correctly serialized/deserialized in save JSON.

Additionally, Origo.GodotAdapter registers these 14 types with the TypedData multi-layer inlining system via `[assembly: SndInlineTypes(startKind: 128, ...)]`, so that runtime `GetKindForType(typeof(Vector3))` returns a deterministic kind value (130), avoiding fallback to the `is T` runtime pattern-matching path. See [Origo.SourceGeneration](../../Origo.SourceGeneration/README.en.md) for details.

## Files

| File | Responsibility |
|------|------|
| `GodotEngineTypeNames.cs` | Stable string constants for 14 Godot engine types |
| `GodotDataSourceConverters.cs` | DataSourceConverter implementations for 14 Godot types |
| `GodotJsonConverterRegistry.cs` | One-stop registration methods: RegisterTypeMappings + RegisterDataSourceConverters |

## Supported Godot Types

| Type | JSON Format | Notes |
|------|----------|------|
| `Vector2` | `{"x":1.0,"y":2.0}` | 2D float vector |
| `Vector2I` | `{"x":1,"y":2}` | 2D integer vector |
| `Vector3` | `{"x":1,"y":2,"z":3}` | 3D float vector |
| `Vector3I` | `{"x":1,"y":2,"z":3}` | 3D integer vector |
| `Vector4` | `{"x":1,"y":2,"z":3,"w":4}` | 4D float vector |
| `Quaternion` | `{"x","y","z","w"}` | Quaternion rotation |
| `Color` | `{"r","g","b","a"}` | RGBA color |
| `Basis` | `{"x":Vec3,"y":Vec3,"z":Vec3}` | 3x3 matrix basis |
| `Transform2D` | `{"x":Vec2,"y":Vec2,"origin":Vec2}` | 2D transform |
| `Transform3D` | `{"basis":Basis,"origin":Vec3}` | 3D transform |
| `Rect2` | `{"position":Vec2,"size":Vec2}` | 2D rectangle |
| `Rect2I` | `{"position":Vec2I,"size":Vec2I}` | 2D integer rectangle |
| `Aabb` | `{"position":Vec3,"size":Vec3}` | Axis-aligned bounding box |
| `Plane` | `{"normal":Vec3,"d":float}` | 3D plane |

## Design Decisions

### Why composite types depend on primitive type converters

`Basis` is composed of 3 `Vector3`s; `Transform3D` is composed of `Basis` + `Vector3`. Reusing existing converters (e.g., `Vector3DataSourceConverter`) avoids duplicate implementation and ensures consistent read/write formats for sub-fields (e.g., all vectors share a uniform x/y/z format).

### Why type names use short forms without namespaces

`"Vector3"` is sufficient to uniquely identify the `Godot.Vector3` type in the Godot context. Short type names reduce JSON storage overhead, while `TypeStringMapping`'s registration mechanism ensures precise mapping to the correct CLR type during deserialization.

### Why Plane's d field uses AsFloat instead of composition

`Plane` = `Normal(Vector3)` + `D(float)`. `D` is a scalar, not a vector; using `AsFloat()` directly is straightforward. A compositional approach (e.g., continuing to use Vector3DataSourceConverter) would introduce unnecessary nesting.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
