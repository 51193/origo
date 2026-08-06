<!-- docsync-pair: Origo.Core/DataSource/Codec/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Codec

> [↑ Back to DataSource](../README.en.md)

## Overview
Concrete codec implementations for `IDataSourceCodec`. All codecs are `internal`, invoked only through `DataSourceIoGateway` by file extension. All file I/O goes through codec routing — zero bypass.

## Included Files

| File | Responsibility |
|------|------|
| `JsonDataSourceCodec.cs` | JSON ↔ DataSourceNode, supports lazy expansion |
| `MapDataSourceCodec.cs` | key:value (`.map`) codec, flat structure, strict mode |
| `RawStringDataSourceCodec.cs` | Raw text ↔ DataSourceNode (`.sha`/`.write_in_progress`) |

## Implementation Details

### JsonDataSourceCodec
- **Lazy expansion on decode**: objects/arrays wrapped as `CreateLazy(rawText, ExpandOneLevel)`; only expanded on access
- **Encode**: recursive traversal via `Utf8JsonWriter`, supports indentation

### MapDataSourceCodec
- Parses `key: value` lines (`#` comments skipped)
- All values are strings; no lazy loading needed
- **Strict mode**: malformed lines (no colon, empty key/value) throw `FormatException` → Gateway wraps as `InvalidOperationException` (fail-fast); duplicate keys do not throw — they log a Warning and the later value wins

### RawStringDataSourceCodec
- Handles `.sha` and `.write_in_progress` as single string nodes
- Ensures codec routing for ALL file content I/O

## Design Decisions

### Why JSON uses lazy expansion
Game saves can be deeply nested (all entities, data, strategies). Lazy expansion amortizes parsing cost.

### Why .map does not support lazy loading
`.map` files are small flat key-value tables (~dozens of lines). No performance benefit.

### Why codecs are internal
Dispatched uniformly by `DataSourceIoGateway`. External code goes through Gateway.

---
[↑ Back to DataSource](../README.en.md)
