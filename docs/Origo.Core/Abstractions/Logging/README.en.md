<!-- docsync-pair: Origo.Core/Abstractions/Logging/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Logging (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Logging](../../Logging/README.en.md)

## Overview
Defines the engine-agnostic basic logging interface `ILogger` and the log level enumeration `LogLevel`. The Core layer only cares about message content and level, not the output destination.

## Included Files

| File | Responsibility |
|------|------|
| `ILogger.cs` | Defines `ILogger` and `ILogger<TCategory>` interfaces, plus `LogLevel` enumeration |

## Interface Details

### LogLevel

| Level | Usage |
|------|------|
| `Debug` | Single-detailed operations (I/O, file reads/writes) |
| `Info` | Key milestones (phase transitions, external call completions) |
| `Warning` | Unexpected but not system-breaking conditions |
| `Error` | Crash-level errors; system cannot continue normal operation |

### ILogger

| Member | Description |
|------|------|
| `Log(level, tag, message)` | Record a log entry with level, tag, and body |

### ILogger\<TCategory\>
Type-aware logging interface; tag is automatically derived from `typeof(TCategory).Name`,
and it inherits `ILogger` (manual-tag scenarios go through the base interface method).

| Member | Description |
|------|------|
| `Log(level, message)` | Record a log with tag auto-set to type name |
| `ILogger.Log(level, tag, message)` | Base interface method (explicit implementation) |

## Design Decisions

### Why log level and interface are in the same file
`LogLevel` is tightly coupled to `ILogger`. Splitting them offers no reuse value.

### Why not provide a formatting method
Formatting is a caller concern. `ILogger` maintains a minimal interface receiving plain strings.

---
[↑ Back to Abstractions](../README.en.md)
