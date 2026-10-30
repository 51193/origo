# Codec

> [↑ Back to DataSource](../README.en.md)

## Overview
Concrete codec implementations for `IDataSourceCodec`, converting between `DataSourceNode` and external data formats (JSON, the custom `.map` format, raw text). All codecs are `internal`, invoked only through `DataSourceIoGateway` by file suffix routing. All file content I/O goes through codec routing — zero bypass.

## Included Files

| File | Responsibility |
|------|------|
| `JsonDataSourceCodec.cs` | JSON ↔ DataSourceNode, supports lazy expansion |
| `MapDataSourceCodec.cs` | key:value (`.map`) codec, flat structure, strict mode (malformed format throws `FormatException`) |
| `RawStringDataSourceCodec.cs` | Raw text ↔ DataSourceNode (`.sha`/`.write_in_progress`), wraps/unwraps the entire file content as a single string node |

## Implementation Details

### JsonDataSourceCodec
- **Lazy expansion on decode**: child nodes of objects/arrays are wrapped as `DataSourceNode.CreateLazy(rawText, ExpandOneLevel)`, avoiding a full parse of large JSON into an in-memory tree at once; the next level is expanded only when accessed via `node[key]` or iteration
- **Encode**: recursive traversal of the DataSourceNode tree via `Utf8JsonWriter`, supports indentation control. The JSON codec uses `System.Text.Json` defaults: decoding accepts up to 64 nested containers and encoding up to 1000. Deeper trees fail explicitly at the codec boundary instead of silently truncating; canonical hashing itself is iterative and has no fixed depth limit.
- **No silent Unicode replacement**: a text value or object key containing an unpaired UTF-16 surrogate is rejected with `InvalidOperationException` before `Utf8JsonWriter` runs. The writer's replacement fallback would otherwise persist U+FFFD instead of the original value, so the save would not round-trip.

### MapDataSourceCodec
- Parses `key: value` line files (lines starting with `#` are treated as comments and skipped)
- All values are strings
- No lazy loading (`.map` files are usually small and flat; no need for laziness)
- Encode outputs keys sorted with `StringComparer.Ordinal` (not node insertion order) and skips null values; only Text children are accepted — Number/Bool children would silently drift into strings on decode, so encode rejects them directly. The ordinal order makes the encoded bytes deterministic for the same tree.
- **Encode round-trip guard**: keys are rejected when empty, have leading/trailing whitespace, start with `#`, or contain `:`/line breaks; values are rejected when they contain line breaks or leading/trailing whitespace. The strict decoder trims both fields, treats the first colon as the separator, and treats lines starting with `#` as comments, so those inputs cannot round-trip losslessly. These rules live in the shared `MapTextFormat` helper, and the save display-metadata merger applies them to contributor output before a save starts so a bad `meta.map` entry fails with contributor context instead of a late encode error
- **Strict decode (no lenient mode)**: malformed lines (no colon, empty key) throw `FormatException` immediately; the Gateway wraps it as an `InvalidOperationException` carrying the file path (fail-fast); duplicate keys do not throw — they log a Warning and the later value wins. **Empty values (`key:` lines) are accepted as empty strings** — the `.map` encoder emits a `key: ` line for empty-string values, so accepting empty values guarantees encode round-trip consistency; empty keys are always rejected
- **No silent Unicode replacement**: keys and values containing an unpaired UTF-16 surrogate are rejected on encode; a fallback encoder would replace the surrogate with U+FFFD and the read-back value would differ

### RawStringDataSourceCodec
- Handles raw text files such as `.sha` and `.write_in_progress`
- Decode wraps the entire file content as a single string value into a `DataSourceNode`
- Encode unwraps the string value from the `DataSourceNode` and writes it to the file; an unpaired UTF-16 surrogate is rejected instead of being persisted as U+FFFD
- Ensures all file content I/O goes through codec routing, eliminating direct-read/direct-write bypasses

## Design Decisions

### Why JSON uses lazy expansion
Game save JSON can be deeply nested (SND scene files contain all entities, data, strategies), but each access usually touches only a few nodes. Lazy expansion spreads the parsing cost to access time, avoiding a full-parse block during initial load.

### Why .map does not support lazy loading
`.map` files (such as `session_topology.map`) are simple flat key-value tables, usually only a few dozen lines. Lazy loading brings no performance benefit in this scenario and only adds implementation complexity.

### Why codecs are internal
Dispatched uniformly by `DataSourceIoGateway`. External code goes through Gateway.

---
[↑ Back to DataSource](../README.en.md)
