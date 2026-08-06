<!-- docsync-pair: Origo.Core/Logging/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Logging

> [↑ Back to Origo.Core](../README.en.md) · [↔ Abstractions: Logging](../Abstractions/Logging/README.en.md)

## Overview

The implementation layer for the `ILogger` interface. Provides two implementations: a production-ready structured log builder `LogMessageBuilder`, and `NullLogger` for testing or silent scenarios.

## Included Files

| File | Responsibility |
|------|---------------|
| `LogMessageBuilder.cs` | Fluent builder for structured log messages (context + elapsed time) |
| `NullLogger.cs` | No-output log implementation, singleton pattern |
| `Logger.cs` | `Logger<T>` generic wrapper: wraps any `ILogger` as `ILogger<T>`, automatically using `typeof(T).Name` as the tag |

## Implementation Details

### LogMessageBuilder

Fluent API, supports chained calls:

```
builder.SetElapsedMs(12.3).AddContext("entity", "player").AddContext("hp", 100).Build("damage applied")
// Output: [+12.30ms] damage applied | entity=player, hp=100
```

- **Elapsed**: Optional, prepended in `[+X.XXms]` format
- **Context**: Appended after the message in ` | key=val, key=val` format
- **Empty value filtering**: Only added if the key is non-whitespace (null values are allowed and rendered as `key=`)
- **Multiple AddContext calls**: Concatenated in addition order, comma-separated; re-adding the same key replaces the existing value (keeping its original position, not appending)

### NullLogger

Singleton implementation; the `Log` method is a no-op body. Used in tests or contexts where logging is not needed, avoiding null checks.

## Design Decisions

### Why LogMessageBuilder uses unified AddContext rather than Prefix/Suffix separation

The prefix/suffix distinction does not provide semantic clarity in practice. A unified context collection (output in addition order) simplifies the API while preserving structured capability.

### Why not provide structured methods directly on ILogger

`ILogger` keeps a minimal interface (only `Log(level, tag, message)`) to accommodate different log backends (Godot's `GD.Print`, file appenders, network logging). Structured construction is a Core-layer value-added service, handled by `LogMessageBuilder`, ultimately producing a plain string.

### Why NullLogger uses a singleton

`NullLogger` has no mutable state; creating multiple instances is a resource waste. Private constructor + static Instance property ensures global uniqueness.

---
[↑ Back to Origo.Core](../README.en.md)
