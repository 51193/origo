# Persistence Flow

> [↑ Back to usage](README.en.md)

## Overview

Origo's save system follows **strict read, explicit failure, two-phase write** contracts. All file I/O goes through `IDataSourceIoGateway`; direct `File.*` API calls are forbidden.

## File Layout

```
{saveRoot}/
├── current/                              # Active save (temporary, runtime state)
│   ├── .write_in_progress                # Write-in-progress marker
│   ├── .payload.sha                     # Combined payload + extra/ SHA-256 (idempotent dedup)
│   ├── progress.json                    # Progress blackboard (global progress + full session topology)
│   ├── progress_state_machines.json      # Progress-level state machine snapshot
│   ├── meta.map                         # Display metadata + format version (when present)
│   ├── extra/                           # Strategy archive files (follow save lifecycle)
│   └── level_{id}/                      # Per-level data
│       ├── snd_scene.json               # Level three-piece set:
│       ├── session.json                 #   - Scene entity list
│       └── session_state_machines.json   #   - Session blackboard
│                                         #   - Session state machines
├── save_001/                            # Snapshot save 1
│   ├── extra/                           # Strategy archive files (follow save lifecycle)
│   └── ... (same structure as above)
├── save_002/                            # Snapshot save 2
│   ├── extra/
│   └── ...
└── save_.../
```

Save IDs use lowercase ASCII letters, digits, `.`, `_`, and `-`; they must not end in `.tmp` or `.bak` (reserved for snapshot staging/backup) and must not be dot-only or end with `.` (Windows strips a trailing period). Mixed-case IDs are rejected because `save_{id}/` is the same directory as a differently-cased ID on default Windows/macOS file systems; level IDs follow the same lowercase, path-safe rule because they name `level_{id}/`. Legacy mixed-case or trailing-period level directories are preserved by the writer for explicit migration and are rejected on load with an error naming the directory and the rename instruction (`level_Town` → `level_town`, plus the matching session topology entry when that level is referenced). A process crash between the two rename steps is recovered when the slot is next listed or read: a complete `save_{id}.bak/` is restored as `save_{id}/`; incomplete staging/backup directories are not exposed as slots.

## Two-Phase Write

The write starts with an idempotent-skip check: when `current/` has no
`.write_in_progress` marker, the target snapshot's `.payload.sha` exists, and it
matches the current payload plus the policy's extra/ side hash, no file is
written.

The combined hash is computed before any file is written, so payload text that
cannot round-trip fails there with no disk side effects: canonical node hashing
rejects an unpaired UTF-16 surrogate (JSON strings cannot carry it losslessly —
the encoder would replace it with U+FFFD), and a mismatched
`SaveGamePayload.SaveId` target is rejected before hashing as well.

The write target id must be non-empty because it names `save_{id}/`. A payload
whose `SaveGamePayload.SaveId` is non-empty must equal the target id: the write
fails with `ArgumentException` when they differ (including a set payload id with
an empty target), and an empty `SaveId` means the target id names the slot.

### Phase 1: Write to current/

1. Create `current/.write_in_progress`.
2. Write `progress.json` and `progress_state_machines.json`.
3. Write `meta.map` with custom display metadata plus `origo.format_version`
   when display metadata exists or the framework format is newer than v1. A
   no-metadata v1 save may omit `meta.map`; the reader maps the missing marker
   to version 1. A framework-reserved `origo.*` key is never exposed as display
   metadata.
4. For each level, write `snd_scene.json`, `session.json`, and
   `session_state_machines.json`. The payload dictionary key must equal the
   level's `LevelId`; level directories not present in the payload are pruned.
5. Write `current/.payload.sha` with the combined payload + extra/ hash.
6. Recreate the marker after the payload is complete. The writer deletes and
   recreates its own marker during this phase; the final state is a protected
   `current/` so a crash before the snapshot cannot expose an unsnapshotted
   write.

### Phase 2: Snapshot to save_{id}/

1. Prepare `save_{id}.tmp/` (delete a stale staging directory first).
2. Recursively copy `current/` into the staging directory. The root
   `.write_in_progress` marker is skipped; a file with that name below `extra/`
   is user data and is copied.
3. If `save_{id}/` exists, delete a stale `.bak` and rename `save_{id}/` to
   `save_{id}.bak/`.
4. Atomically rename `save_{id}.tmp/` to `save_{id}/`. If the rename fails, try
   to restore `.bak/` to `save_{id}/`; if rollback also fails, keep `.bak/` and
   report both failures.
5. After the new snapshot is installed, delete `save_{id}.bak/`.
6. After the snapshot succeeds, delete `current/.write_in_progress`. If the
   snapshot fails, leave the marker in place so the next read of `current/` is
   rejected; the failure is logged and rethrown.

The backup is never deleted before the new snapshot is installed. A crash
between the two renames leaves a complete `.bak` that the next list or direct
snapshot read restores as the real slot; `.tmp` is never listed as a save.

## Strict Read Rules

| Condition | Behavior |
|-----------|----------|
| `current/` has `.write_in_progress` | Throw `InvalidOperationException` (interrupted write; must be handled first) |
| All three level files missing | Treated as "no save for this level" (`null`) |
| Level three files partially present | Throw exception (data corruption) |
| `progress.json` missing on a full payload read | Throw exception; the narrow `ReadProgressNodeFromSnapshot` probe returns `null` |
| Format version below 1 or newer than supported | Throw exception (0/negative is corrupt data; a newer version cannot be parsed) |
| A level-directory name is not a valid level identity | Throw exception naming the legacy director(y/ies) and instructing the lowercase/path-safe rename; do not silently skip legacy data |
| Topology and payload levels disagree | Throw exception (a referenced level has no payload, or a payload level is not referenced) |
| No level directories at all | Not a valid written save (the writer requires the active level); a full payload read fails on the missing active level. "No saves yet" is the business-layer signal from `ListSaves`/`HasContinueData`, not a reader result. |

## JSON Format Reference

### progress.json

The progress blackboard is a flat map where each key is a `TypedData` value. `origo.session_topology` is a required key.

```json
{
  "origo.session_topology": {
    "type": "String",
    "data": "__foreground__=default=false"
  }
}
```

### session.json

The session blackboard uses the same format as progress: a flat map `{key: TypedData}` structure. An empty blackboard is written as `{}`.

```json
{
  "high_score": { "type": "Int32", "data": 150 }
}
```

### snd_scene.json

Scene entities are a JSON array of `SndMetaData`. Each element contains `name`, `node`, `strategy`, `data`.

```json
[
  {
    "name": "Player",
    "node": { "pairs": { "root": "player" } },
    "strategy": {
      "lifecycle_indices": ["game.player_control"],
      "active_indices": [],
      "observer_indices": []
    },
    "data": {
      "pairs": {
        "hp": { "type": "Int32", "data": 100 },
        "position": { "type": "Single", "data": 320.0 }
      }
    }
  }
]
```

### TypedData Value Format

```json
{ "type": "TypeName", "data": value }
```

- `Int32` / `Single` / `Boolean` / `String` → JSON primitive type values
- Array types → JSON arrays

> **Note**: Blackboard TypedData is a flat map (no `pairs` wrapper), while entity data in `SndMetaData` and templates uses the `{ "pairs": { key: TypedData } }` nested structure. The two must not be confused.

## Save Structure

### SaveGamePayload

```csharp
SaveGamePayload {
    SaveId,                     // Save slot ID (must equal a non-empty write target when set; empty lets the target name the slot)
    ActiveLevelId,              // Current active level
    ProgressNode,               // Progress blackboard: global progress + session topology (DataSourceNode)
    ProgressStateMachinesNode,  // Progress-level state machines
    CustomMeta,                 // Display metadata dictionary
    Levels: {                   // All level data (key = levelId; no duplicate levelId entries — the framework validates levelId uniqueness before building the payload)
        {levelId}: LevelPayload {
            LevelId,
            SndSceneNode,               // Scene entity list
            SessionNode,                // Session blackboard
            SessionStateMachinesNode    // Session state machines
        }
    }
}
```

> **Node ownership**: Every `DataSourceNode` tree inside a `SaveGamePayload` / `LevelPayload` must be disposed by its owner. The framework releases temporary payloads it creates at internal load/mount and write/snapshot boundaries; payloads returned by public `ISaveStorageService` read methods are owned by the caller, and write methods must consume their node trees during the call.

## Save API

### Requesting a Save

```csharp
// In a strategy:
ctx.Save.RequestSaveGame("my_save_id"); // lowercase ASCII letters/digits/._-, no .tmp/.bak suffix
ctx.Save.RequestSaveGameAuto();  // Auto-generate timestamp ID

// ProgressRun handles these requests:
// - Collect Progress blackboard
// - Collect Session blackboards (trigger strategies' final modifications via BeforeSave hooks)
// - Collect SND scene (via BuildMetaList)
// - Build SaveGamePayload
// - Two-phase write
```

### Requesting a Load

```csharp
ctx.Save.RequestLoadGame("my_save_id");        // Load a specific slot
ctx.Lifecycle.RequestContinueGame();           // Try to continue the game
ctx.Lifecycle.RequestLoadInitialSave();        // Load the initial save
ctx.Lifecycle.RequestLoadMainMenuEntrySave();  // Load the main menu entry save
```

### Enumerating Saves

```csharp
// Get all save slot IDs
var ids = ctx.Save.ListSaves();

// Get save slots + display metadata (for save selection UI)
var entries = ctx.Save.ListSavesWithMetaData();
// entries[i].SaveId → "001"
// entries[i].MetaData → { "play_time": "2h30m", "level": "town" }
```

### Deleting a Save

```csharp
// Synchronously delete an inactive slot and its .tmp/.bak remnants
ctx.Save.DeleteSave("old_slot");
```

Deletion rejects the active save, the continue target, and any state where a persistence request is executing or pending; a missing slot throws `InvalidOperationException` explicitly. Storage-level behavior is provided by `ISaveStorageService.DeleteSave`.

### Observing Persistence Completion

```csharp
// Whether a save/load/continue/switch/bootstrap-entry request is executing or pending
bool persistenceIdle = ctx.Deferred.IsPersistenceIdle;

// Whether the main-menu entry enqueued by Bootstrap() mounted successfully
bool bootstrapped = ctx.Lifecycle.IsBootstrapCompleted;
```

These properties are observation-only; they provide no wait, flush, or execution entry point. Callers still advance the frame thread through `IOrigoFrameDriver.DriveFrame`. When an entry load fails, `DriveFrame` propagates the exception under fail-fast; `IsBootstrapCompleted` stays false while the request count still returns to zero on the exception path.

## meta.map Display Metadata

Display metadata is separated from business data. `meta.map` only stores the minimal summary information needed for the save selection UI:

```
save_id: my_save_001
level_name: Town
play_time: 2h30m
player_name: Alice
```

Custom metadata is contributed through the `ISaveMetaContributor` interface, registered via `ctx.Save.RegisterSaveMetaContributor(...)`:

```csharp
// Contributor implementation
class MySaveMetaContributor : ISaveMetaContributor
{
    public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context)
    {
        return new Dictionary<string, string>
        {
            ["play_time"] = CalculatePlayTime(),
            ["player_name"] = context.Progress?.TryGet<string>("player_name").value ?? ""
        };
    }
}

// Registration (in OrigoDefaultEntry.ConfigureSaveMetadataContributors or in a strategy)
ctx.Save.RegisterSaveMetaContributor(new MySaveMetaContributor());
// A delegate overload can also be used:
ctx.Save.RegisterSaveMetaContributor((context) => new Dictionary<string, string>
{
    ["custom"] = "value"
});
```

## Path Policy

`ISavePathPolicy` controls directory and file layout. It is a **Kernel-only extension point**: it is not part of the published shell API, no shell/adapter host option accepts a custom policy, and package consumers use the default layout. The shape below documents the repository implementation contract; do not depend on it from package-only game code.

```csharp
public interface ISavePathPolicy
{
    string GetCurrentDirectory();
    string GetSaveDirectory(string saveId);
    string GetProgressFile(string baseDir);
    string GetProgressStateMachinesFile(string baseDir);
    string GetCustomMetaFile(string baseDir);
    string GetLevelDirectory(string baseDir, string levelId);
    string GetLevelSndSceneFile(string levelDirectory);
    string GetLevelSessionFile(string levelDirectory);
    string GetLevelSessionStateMachinesFile(string levelDirectory);
    string GetWriteInProgressMarker(string baseDir);
    string GetPayloadShaFile(string baseDir);
    string GetExtraDirectory(string baseDir);
    IReadOnlyList<string> EnumerateSaveIds(IFileMetaAccess metaAccess, string saveRootPath);
    IReadOnlyDictionary<string, string> EnumerateInterruptedSnapshots(
        IFileMetaAccess metaAccess,
        string saveRootPath);
    string? TryGetLevelIdFromDirectory(string directoryPath);
}
```

`EnumerateSaveIds` is the slot enumeration hook for a custom layout; `EnumerateInterruptedSnapshots` maps each save ID to the actual backup directory path used by interrupted-snapshot recovery, and `TryGetLevelIdFromDirectory` is the inverse mapping used when discovering level payloads and checking snapshot completeness. The default implementations recognize the standard `save_`, `level_`, and `.bak` naming; a custom policy overrides the applicable hooks so `ListSaves`, `ListSavesWithMetaData`, direct loads, and recovery see the same slots and levels that writes created. `GetExtraDirectory` is also used by extra-file restore and the idempotency side hash, so a policy that renames `extra/` remains consistent across save and load.

Directory/slot members may rename any directory segment. File-path members must keep the suffix that routes their content type (`.json`, `.map`, `.sha`, `.write_in_progress`): all payload file content goes through `IDataSourceIoGateway` suffix routing, so a custom policy that renames `progress.json` to another suffix makes every subsequent write/read fail with an explicit "no codec configured" error.

The default implementation `DefaultSavePathPolicy` → `SavePathLayout` provides the standard layout.

## Related Documents

- [Session Model](session-model.en.md) — Relationship between Session and saves
- [Architecture Overview](../architecture/overview.en.md) — Persistence's position in the overall architecture

---
[↑ Back to usage](README.en.md)
