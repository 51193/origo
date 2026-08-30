<!-- docsync-pair: usage/persistence-flow -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Persistence Flow

> [↑ Back to usage](README.en.md)

## Overview

Origo's save system follows **strict read, explicit failure, two-phase write** contracts. All file I/O goes through `IDataSourceIoGateway`; direct `File.*` API calls are forbidden.

## File Layout

```
{saveRoot}/
├── current/                              # Active save (temporary, runtime state)
│   ├── .write_in_progress                # Write-in-progress marker
│   ├── progress.json                    # Progress blackboard (global progress + full session topology)
│   ├── progress_state_machines.json      # Progress-level state machine snapshot
│   ├── meta.map                         # Display metadata (save list)
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

## Two-Phase Write

### Phase 1: Write to current/

```
1. Create current/.write_in_progress marker
2. Write progress.json
3. Write progress_state_machines.json
4. Write meta.map (if custom metadata exists)
5. For each level:
   a. Write level_{id}/snd_scene.json
   b. Write level_{id}/session.json
   c. Write level_{id}/session_state_machines.json
6. Delete the save_{id}.bak/ backup
7. Delete .write_in_progress marker
```

### Phase 2: Snapshot to save_{id}/

```
1. Recreate current/.write_in_progress marker
2. Create save_{id}.tmp/
3. Recursively copy all files from current/ to save_{id}.tmp/
4. If save_{id}/ already exists, rename it aside to save_{id}.bak/ (the old data is never deleted before the new data is in place)
5. Atomically rename save_{id}.tmp/ → save_{id}/
6. Delete .write_in_progress marker
```

If Phase 2 fails, `current/` data is intact but the marker remains. The next read of `current/` will be rejected due to the marker, and logs will indicate manual intervention or retry is needed.

## Strict Read Rules

| Condition | Behavior |
|-----------|----------|
| `current/` has `.write_in_progress` | Throw `InvalidOperationException` (interrupted write; must be handled first) |
| All three level files missing | Treated as "no save for this level" (`null`) |
| Level three files partially present | Throw exception (data corruption) |
| `progress.json` missing | Throw exception |
| No levels exist at all | Treated as "no save exists yet" |

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
    SaveId,                     // Save slot ID
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

## Save API

### Requesting a Save

```csharp
// In a strategy:
ctx.Save.RequestSaveGame("my_save_id");
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

`ISavePathPolicy` controls directory and file layout, and is replaceable to suit different platforms:

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
}
```

The default implementation `DefaultSavePathPolicy` → `SavePathLayout` provides the standard layout.

## Related Documents

- [Session Model](session-model.en.md) — Relationship between Session and saves
- [Architecture Overview](architecture-overview.en.md) — Persistence's position in the overall architecture

---
[↑ Back to usage](README.en.md)
