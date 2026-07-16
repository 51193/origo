<!-- docsync-pair: Origo.TestSupport/Logging/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Logging

> [↑ Back to TestSupport](../README.en.md)

## Overview

In-memory test double for `ILogger` that collects messages by severity level for verifying log output.

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestLogger.cs` | Implements `ILogger`, categorizing messages by level into four public lists: `Debugs`, `Infos`, `Warnings`, `Errors`. Supports `MinimumLevel` filtering. |

## Usage Pattern

```csharp
var logger = new TestLogger();
logger.Log(LogLevel.Error, "tag", "something broke");
Assert.Contains("something broke", logger.Errors);
```

---

[↑ Back to TestSupport](../README.en.md)
