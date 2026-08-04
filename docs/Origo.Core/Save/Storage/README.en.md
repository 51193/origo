<!-- docsync-pair: Origo.Core/Save/Storage/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6. -->
# Storage

> [↑ Back to Save](../README.en.md)

## Overview

Complete implementation of the save storage layer. Responsible for file I/O (read/write, directory management, snapshots), path layout strategy, and Payload construction. All file operations go through `IFileMetaAccess` + `IDataSourceIoGateway` + `IPathResolver`; no direct `File.*` API calls (`IFileSystem` is internalized).

## Included Files

| File | Responsibility |
|------|---------------|
| `ISaveStorageService.cs` | Save read/write service public interface (cross-assembly) |
| `ISavePathPolicy.cs` | Save path policy interface (replaceable layout) |
| `DefaultSaveStorageService.cs` | Default ISaveStorageService implementation, internally delegates to SaveFileHandle + SavePayloadWriter/Reader |
| `DefaultSavePathPolicy.cs` | Default ISavePathPolicy implementation, delegates to SavePathLayout |
| `SaveFileHandle.cs` | Unified I/O context: encapsulates IFileMetaAccess + IDataSourceIoGateway + IPathResolver + saveRootPath + ISavePathPolicy, combining the path utility methods of the former SavePathResolver and the gateway creation of SaveStorageGatewayFactory |
| `SavePathLayout.cs` | Standard path layout constants and methods (current/, save_*, level_*) |
| `SavePayloadWriter.cs` | Save write orchestration (two-phase write + marker management) |
| `SavePayloadReader.cs` | Save read orchestration (strict reading + integrity validation) |
| `SaveGamePayloadFactory.cs` | Constructs SaveGamePayload (business data aggregation) |
| `SaveStorageFacade.cs` | Save I/O orchestration layer (internal static): EnumerateSaveIds / EnumerateSavesWithMetaData / read/write orchestration / snapshot copy. Pure orchestration logic; concrete file parsing/serialization delegated to SavePayloadReader / SavePayloadWriter; atomic write logic delegated to SaveAtomicWriter |
| `SaveAtomicWriter.cs` | Atomic write helper (internal static): SHA-256 idempotent dedup, write-in-progress marker management, temp directory preparation, backup-replace snapshot swap. Called exclusively by SaveStorageFacade |

## File Layout

```
{saveRoot}/
├── current/                          # Active save directory
│   ├── .write_in_progress            # Write interruption marker
│   ├── .payload.sha                  # Payload SHA-256 digest (idempotent dedup)
│   ├── progress.json                # Progress blackboard
│   ├── progress_state_machines.json  # Progress-level state machines
│   ├── meta.map                     # Display metadata
│   └── level_{id}/                  # Per-level saves
│       ├── snd_scene.json           # Three-piece set
│       ├── session.json
│       └── session_state_machines.json
├── save_001/                        # Snapshot save slot
│   └── ... (same structure as above)
└── save_002/
    └── ...
```

## Two-Phase Write Flow

1. **Idempotency check** (only at `WriteSavePayloadToCurrentThenSnapshot` entry): if the target snapshot `save_{id}/.payload.sha` exists and the hash matches, return immediately (skip write)
2. **Integrity validation**: validate the payload before writing any file — active level must exist in `Levels`, progress node must be non-null. Validation failure throws immediately; no half-written `current/` is produced
3. **Write marker**: create `.write_in_progress` under `current/`
4. **Write payload**: write progress.json, per-level three-piece sets, meta.map, `.payload.sha` (all completed under marker protection)
5. **Clear phase-1 marker**: after `current/` is fully written (including `.payload.sha`), delete the marker
6. **Recreate marker**: rebuild marker for the snapshot phase; if snapshot fails, marker remains so subsequent reads will reject this "updated but not snapshotted" `current/`
7. **Snapshot (backup-replace)**: copy `current/` to `save_{id}.tmp/` → rename existing `save_{id}/` to `save_{id}.bak/` → rename `.tmp` to the final `save_{id}/` → delete `.bak`. Old data is not deleted until new data is in place
8. **Clear marker**: delete the marker

## Strict Read Rules

- **`.write_in_progress` exists under `current/`** → refuse to read, throw exception (previous write was interrupted)
- **Per-level three-piece set incomplete** → refuse to read (partial existence = corruption)
- **progress.json missing** → refuse to read

## Design Decisions

### Why two-phase write

`current/` is written first to ensure data lands; the snapshot phase copies verified complete data to persistent slots. If the snapshot phase fails, `current/` still retains complete data (but the marker remains, causing the next read to reject it with a log notification). A situation of "snapshot slot has data but `current/` was lost due to crash" will never occur.

### Why not use temp file + rename for atomic single-file writes

Saves involve multiple files (progress.json + 3 files per N levels). Atomic rename of a single file cannot guarantee multi-file consistency. The `.write_in_progress` marker serves as a transaction marker for the entire save directory.

### Why ISavePathPolicy is replaceable

Different platforms (desktop, mobile, cloud save) may require different path layouts. Injecting the layout policy into `DefaultSaveStorageService` rather than hardcoding allows platform-specific policies to be injected at the adapter layer.

### Why SaveFileHandle uniformly encapsulates I/O dependencies

`SaveFileHandle` encapsulates the four I/O dependencies `(IFileSystem, IDataSourceIoGateway, string saveRootPath, ISavePathPolicy)` into a single parameter object, making methods in SavePayloadReader/SavePayloadWriter/SaveStorageFacade map one-to-one with their implementations, avoiding the multi-level overload chains needed for passing the four-piece set layer by layer. `DefaultSaveStorageService` internally holds only a single field. It simultaneously carries path utility methods and gateway creation logic without needing separate helper classes.

### Why use SHA digest for idempotent deduplication

When the same game state is written to the same save slot multiple times, SHA-256 digest comparison avoids unnecessary I/O. At the `WriteSavePayloadToCurrentThenSnapshot` entry point, the target snapshot's `.payload.sha` is compared against the hash of the payload to be written:

- **Hash matches** → log INFO "idempotent save skip", return immediately, no file operations performed
- **Hash differs or .sha does not exist** → proceed with normal two-phase write flow

`ComputePayloadHash` computes SHA individually for each component's node tree and combines them, ensuring:
- Progress blackboard change → hash differs → rewrite
- Any level data change → hash differs → rewrite
- CustomMeta key-value change → hash differs → rewrite
- Only SaveId differs (writing to a different slot) → always write (new slot has no .sha to compare)

The `current/.payload.sha` writes follow three deliberate, non-conflicting semantics: the snapshot path (`WriteSavePayloadToCurrentThenSnapshot`) writes the **combined hash** (payload + `extra/`), which the next idempotency comparison consumes; the test path (`SaveStorageFacade.WriteSavePayloadToCurrent`) writes the **payload-only hash** (tests have no `extra/` side channel, so a combined hash would be stale); the load-recovery path (`DefaultSaveStorageService.WriteSavePayloadToCurrent`) writes **no hash** — a recovery write has no idempotency contract, and only the snapshot phase performs deduplication. The only consumer of `.payload.sha` is `TryIdempotentSkip` comparing against the snapshot directory; the `current/` hash itself is consumed only indirectly when it is copied into a snapshot.

### Why DataSourceNode computes a Canonical Hash rather than a post-serialization hash

`DataSourceNode.ComputeSha256Hash()` recursively generates a deterministic string representation and then applies SHA-256. Unlike the codec serialization approach, the canonical string does not depend on the codec version or indentation configuration; keys are sorted in dictionary order, ensuring the same data tree always produces the same hash.

---

[↑ Back to Save](../README.en.md)
