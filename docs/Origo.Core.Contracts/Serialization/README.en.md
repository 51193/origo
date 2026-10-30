# Serialization

> [↑ Back to Origo.Core.Contracts](../README.en.md) · [↔ DataSource: Converters](../../Origo.Core.Kernel/DataSource/Converters/README.en.md)

## Overview
Type-to-string mapping infrastructure. During JSON serialization, `TypedData` needs precise CLR type info for correct deserialization. `TypeStringMapping` maintains bidirectional type ↔ stable string identifier mapping, avoiding full type names in JSON.

## Included Files

| File | Responsibility |
|------|------|
| `BclTypeNames.cs` | Stable string constants for the BCL primitives, their primitive arrays, and the immutable string collection types |
| `TypeStringMapping.cs` | Bidirectional type ↔ string mapping table; registers all known types at startup |

## Implementation Details

### BclTypeNames
Uses short names (`"Int32"`, `"ArraySingle"`) rather than full CLR names, reducing JSON size.

### TypeStringMapping
- **Constructor**: Pre-registers the BCL primitive, primitive-array, and immutable collection type names
- **RegisterType<T>()**: Public, for adapter layers to register engine-specific types
- **Bidirectional validation**: Checks for conflicts on registration; throws `InvalidOperationException`
- **Lookup failures throw**: Never returns null

## Design Decisions

### Why not use Type.FullName
`Type.FullName` changes with namespace and assembly version. Stable string mappings decouple type identity from code structure.

### Why no "System." prefix
Names like `"Int32"` suffice for disambiguation. Simplification reduces JSON storage.

### Why lookup failures throw immediately
Type mapping is the backbone of serialization. Unknown type name means format error or incompatibility — fail fast.

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
