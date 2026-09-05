<!-- docsync-pair: Origo.TestSupport/Snd/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Snd

> [↑ Back to TestSupport](../README.en.md)

## Overview

SND offline-building tooling in the test-support library. `LevelBuilder` moved here from the Core production assembly; tests and tooling use it to construct `LevelPayload` fluently without entering the framework's production runtime path.

## Files

| File | Responsibility |
|------|----------------|
| `LevelBuilder.cs` | internal fluent level builder: `AddEntity` / `AddEntityFromTemplate` / `AddEntities` / `SetSessionData` → `Build()` produces a `LevelPayload`, or `Commit()` writes it directly to the storage service's `current/`. Uses `StubSndSceneHost` as its zero-dependency scene container |

## Usage

```csharp
var builder = new LevelBuilder("lvl_1", sndWorld, storageService)
    .AddEntityFromTemplate("npc", "guard")
    .SetSessionData("difficulty", 2);
var payload = builder.Build();
```

Business code should still build runtime levels through templates and `entry.json`; `LevelBuilder` targets test and offline tooling scenarios.

---

[↑ Back to TestSupport](../README.en.md)
