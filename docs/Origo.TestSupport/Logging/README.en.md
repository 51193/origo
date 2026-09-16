<!-- docsync-pair: Origo.TestSupport/Logging/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->

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
