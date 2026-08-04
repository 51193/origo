<!-- docsync-pair: Origo.Core/Save/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Save

> [↑ Back to Origo.Core](../README.en.md)
> [↔ Related Tests: Save-Storage](../../Origo.Core.Tests/Save-Storage.en.md) · [Save-Serialization](../../Origo.Core.Tests/Save-Serialization.en.md) · [Save-Meta](../../Origo.Core.Tests/Save-Meta.en.md)

## Module Capability

Origo's persistence system. Responsible for the complete save lifecycle: payload construction, file read/write (two-phase write), snapshot management, path layout policy, and display metadata collection. Follows the "strict read, explicit failure, two-phase write" persistence contract.

## Sub-Modules

| Sub-Module | Capability | Details |
|-----------|-----------|---------|
| [Meta](Meta/README.en.md) | Display metadata construction and merging | ISaveMetaContributor + SaveMetaMerger + meta.map codec |
| [Serialization](Serialization/README.en.md) | Save serialization orchestration | BlackboardSerializer + SndSceneSerializer + SaveContext |
| [Storage](Storage/README.en.md) | Storage layer complete implementation | Two-phase write, strict read, path layout, snapshot management |

## This Layer's Core Files

| File | Responsibility |
|------|---------------|
| `PersistentBlackboard.cs` | Persistent blackboard: auto loads/saves from disk; uses atomic write (temp file + rename) on every mutation to prevent file corruption on crash. Stale temp files from interrupted writes are cleaned up on load. |
| `SavePayloads.cs` | Save payload model: `SaveGamePayload` / `LevelPayload` / serialization containers |
| `WellKnownKeys.cs` | `internal` — Blackboard key constants: `SessionTopology` / `ActiveSaveId`, etc. |
| `SaveCoordinator.cs` | Save coordinator: an independent class responsible for building save payloads, persisting progress state, managing metadata |
| `SaveFileHandle.cs` | Unified I/O context (in the Storage sub-module): encapsulates FileSystem + IoGateway + SaveRootPath + PathPolicy |

## Persistence Flow

```
ISndSaveOperations.RequestSaveGame(saveId)
    │
    ▼
SaveCoordinator.BuildSavePayload(...)
    ├── BuildSaveMetaContext()
    │       └── Collect SaveMetaBuildContext (saveId, levelId, blackboard, scene)
    ├── SerializeProgress()  →  progress.json
    ├── SerializeSession()   →  session.json
    └── BuildSndScene()  →  snd_scene.json
    │
    ▼
SaveGamePayload (complete save object)
    │
    ▼
SavePayloadWriter.WriteToCurrent(handle, payload)
    ├── Create .write_in_progress marker
    ├── Write current/progress.json + progress_state_machines.json
    ├── Write current/level_*/snd_scene.json
    ├── Write current/level_*/session.json
    ├── Write current/level_*/session_state_machines.json
    ├── Write current/meta.map
    └── Delete .write_in_progress
    (current/.payload.sha is written separately by SaveAtomicWriter.WritePayloadSha, recording the combined hash)
    │
    ▼
DefaultSaveStorageService.WriteSavePayloadToCurrentThenSnapshot(...)
    ├── Check if save_{id}/.payload.sha exists with identical hash → skip (idempotent dedup)
    ├── Recreate .write_in_progress marker
    ├── Copy current/ → save_{id}.tmp/
    ├── Delete old save_{id}/ (if exists)
    ├── Rename save_{id}.tmp/ → save_{id}/
    └── Delete .write_in_progress marker
```

## Strict Read Rules

- **current/ has `.write_in_progress`** → throw exception (previous write interrupted; needs handling)
- **Level three files partially present** → throw exception (data corruption)
- **progress.json missing** → throw exception
- **All missing** → treated as "no save exists yet" (legal state)

---
[↑ Back to Origo.Core](../README.en.md)
