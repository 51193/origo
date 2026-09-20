<!-- docsync-pair: Origo.Core.Contracts/DataSource/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# DataSource

> [↑ Back to Origo.Core.Contracts](../README.en.md) · [↔ Implementation: Origo.Core/DataSource](../../Origo.Core/DataSource/README.en.md)

## Module Capability

Data-source contract layer: tree data model, I/O gateway contract, file-meta
contract, and converter base classes. Codecs, the factory, the registry, and
concrete converters live in [Origo.Core/DataSource](../../Origo.Core/DataSource/README.en.md).

## Included Files

| File | Responsibility |
|------|----------------|
| `DataSourceNode.cs` | Tree data node: Map/Array/Text/Number/Bool/Null + lazy expansion + `As<T>()` + Builder `Add` + shape-strict `Keys`/`Elements` + `ComputeSha256Hash()` |
| `DataSourceNodeKind.cs` | Node type enum |
| `DataSourceConverter.cs` | Converter bases: `DataSourceConverterBase` and `DataSourceConverter<T>` |
| `IDataSourceIoGateway.cs` | I/O gateway contract: `ReadTree` / `WriteTree` |
| `IFileMetaAccess.cs` | File-meta contract: existence, enumeration, directories, delete, copy, rename |

## Design Decisions

### Why shape access on DataSourceNode is strict

`Keys` / `Elements` throw `InvalidOperationException` for non-Map/non-Array
nodes instead of silently yielding an empty collection. Save reads are a strict
path, so a wrong shape must fail explicitly.

### Why the node tree is IDisposable

Lazy expansion holds child nodes and closure resources. `Dispose()` uses an
iterative traversal so deeply nested trees do not overflow the stack; callers
own node trees at save boundaries and must release them.

### Why converter bases live in Contracts

`DataSourceConverter<T>` / `DataSourceConverterBase` are consumer and adapter
extension points; the concrete registry and default converters stay in the
implementation package, and the contract layer contains no runtime
construction logic.

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
