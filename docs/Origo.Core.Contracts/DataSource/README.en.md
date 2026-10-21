# DataSource

> [↑ Back to Origo.Core.Contracts](../README.en.md) · [↔ Implementation: Origo.Core.Kernel/DataSource](../../Origo.Core.Kernel/DataSource/README.en.md)

## Module Capability

Data-source contract layer: tree data model, I/O gateway contract, file-meta
contract, converter base classes, and the shared converter registry. Codecs,
the factory, and concrete converters live in
[Origo.Core.Kernel/DataSource](../../Origo.Core.Kernel/DataSource/README.en.md).

## Included Files

| File | Responsibility |
|------|----------------|
| `DataSourceNode.cs` | Tree data node: Map/Array/Text/Number/Bool/Null + lazy expansion + `As<T>()` + Builder `Add` + shape-strict `Keys`/`Elements` + `ComputeSha256Hash()` |
| `DataSourceNodeKind.cs` | Node type enum |
| `DataSourceConverter.cs` | Converter bases: `DataSourceConverterBase` and `DataSourceConverter<T>` |
| `IDataSourceIoGateway.cs` | I/O gateway contract: `ReadTree` / `WriteTree` |
| `IFileMetaAccess.cs` | File-meta contract: existence, enumeration, directories, delete, copy, rename |
| `DataSourceConverterRegistry.cs` | Shared registry for `DataSourceConverter<T>` instances, used by shell tooling and kernel services |

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
extension points. `DataSourceConverterRegistry` is also a Contracts type because
consumers and shell tooling need to compose converters; the default converters
and codecs stay in the implementation package, and the contract layer contains
no runtime construction logic.

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
