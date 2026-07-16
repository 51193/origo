<!-- docsync-pair: Origo.TestSupport/Scene/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Scene

> [↑ Back to TestSupport](../README.en.md)

## Overview

Test double for `ISndSceneHost`. Manages entities in an in-memory list with a built-in `DummySndEntity` (minimal `ISndEntity` implementation).

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestSndSceneHost.cs` | Implements `ISndSceneHost`, managing entities in `List<ISndEntity>`. Records all `CreateEntity` invocation history (`MetaList` property) and `ClearAllCount`. The built-in `DummySndEntity` provides minimal Name and metadata support. |

## Usage Pattern

```csharp
var host = new TestSndSceneHost();
var entity = host.CreateEntity(new SndMetaData { Name = "test" });
Assert.Single(host.GetEntities());
```

---

[↑ Back to TestSupport](../README.en.md)
