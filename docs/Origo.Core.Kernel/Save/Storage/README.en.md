# Storage

> [↑ Back to Save](../README.en.md)

## Overview

Complete implementation of the save storage layer. Responsible for file I/O (read/write, directory management, snapshots), path layout strategy, and Payload construction. All file operations go through `IFileMetaAccess` + `IDataSourceIoGateway` + `IPathResolver`; no direct `File.*` API calls (`IFileSystem` is an adapter-layer extension point injected by the host).

## Included Files

| File | Responsibility |
|------|---------------|
| `ISaveStorageService.cs` | Save read/write/delete service public interface (cross-assembly) |
| `ISavePathPolicy.cs` | Save path policy interface (replaceable layout, slot/level discovery, and interrupted-snapshot enumeration) |
| `DefaultSaveStorageService.cs` | Default ISaveStorageService implementation; delegates snapshot reads to the recovery-aware facade and other I/O to SaveFileHandle + SavePayloadWriter/Reader |
| `DefaultSavePathPolicy.cs` | Default ISavePathPolicy implementation, delegates to SavePathLayout |
| `SaveFileHandle.cs` | Unified I/O context: encapsulates IFileMetaAccess + IDataSourceIoGateway + IPathResolver + saveRootPath + ISavePathPolicy, combining path utility methods and gateway creation |
| `SavePathLayout.cs` | Standard path layout constants and methods (current/, save_*, level_*) |
| `SavePayloadWriter.cs` | Save write orchestration (two-phase write + marker management) |
| `SavePayloadReader.cs` | Save read orchestration (strict reading + integrity validation) |
| `SaveGamePayloadFactory.cs` | Constructs SaveGamePayload (business data aggregation) |
| `SaveStorageFacade.cs` | Save I/O orchestration layer (internal static): EnumerateSaveIds / EnumerateSavesWithMetaData / read/write orchestration / slot and remnant deletion / snapshot copy. Pure orchestration logic; concrete file parsing/serialization delegated to SavePayloadReader / SavePayloadWriter; atomic write logic delegated to SaveAtomicWriter |
| `SaveAtomicWriter.cs` | Atomic write helper (internal static): SHA-256 idempotent dedup, write-in-progress marker management, temp directory preparation, backup-replace snapshot swap. Called exclusively by SaveStorageFacade |

## File Layout

```
{saveRoot}/
├── current/                          # Active save directory
│   ├── .write_in_progress            # Write interruption marker
│   ├── .payload.sha                  # Payload SHA-256 digest (idempotent dedup)
│   ├── progress.json                # Progress blackboard
│   ├── progress_state_machines.json  # Progress-level state machines
│   ├── meta.map                     # Display metadata + format version (when present)
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

1. **Idempotency check** (only at `WriteSavePayloadToCurrentThenSnapshot` entry): if `current/` has no `.write_in_progress` marker, the target snapshot `save_{id}/.payload.sha` exists, and the hash matches, return immediately (skip write)
2. **Write marker**: create `.write_in_progress` under `current/`
3. **Integrity validation**: validate the payload before writing any payload file — active level must exist in `Levels`, progress node must be non-null. A validation failure leaves the marker in place, so `current/` is rejected by readers until the next write instead of being mistaken for a valid checkpoint
4. **Write payload**: write progress.json, per-level three-piece sets, and meta.map (with `origo.format_version` plus any display metadata; a no-metadata v1 save may omit it); `.payload.sha` is written after the payload completes, before the marker is recreated (combined hash = payload + `extra/`)
5. **Clean up stale level directories**: delete `level_*` directories under `current/` that are not in the payload (e.g. levels of destroyed sessions). The payload is the authoritative set of active levels; cleanup keeps `current/` consistent with the payload and prevents stale data from being copied into every snapshot (this step runs within marker protection; failure rejects reads)
6. **Clear phase-1 marker**: after the payload files and stale-level cleanup, `WriteToCurrent` deletes its marker
7. **Write `.payload.sha` and recreate the marker**: `WriteCurrentWithMarker` writes the combined hash and then rebuilds the marker for the snapshot phase; if the snapshot fails, that marker remains so subsequent reads reject this "updated but not snapshotted" `current/`
8. **Snapshot (backup-replace)**: copy `current/` to `save_{id}.tmp/` → rename existing `save_{id}/` to `save_{id}.bak/` → rename `.tmp` to the final `save_{id}/` → delete `.bak`. Old data is not deleted until new data is in place; if the temp-to-final rename fails, the code attempts to roll `.bak` back to `save_{id}/`, and keeps the `.bak` copy when rollback fails
9. **Validate staged copy**: require `progress.json` and `progress_state_machines.json` at the root of `save_{id}.tmp/`; on failure delete the temp copy and keep the existing slot (the marker remains)
10. **Clear marker**: delete the marker (the one recreated for the snapshot phase)

## Strict Read Rules

- **`.write_in_progress` exists under `current/`** → refuse to read, throw exception (previous write was interrupted)
- **Per-level three-piece set incomplete** → refuse to read (partial existence = corruption)
- **A level directory whose name is not a valid level identity** → refuse to read with a migration error that names the offending director(y/ies) and instructs the user to rename them to lowercase/path-safe ids; the writer preserves legacy mixed-case/trailing-period directories for explicit migration, and the reader must not silently skip them or surface a parameter-validation error
- **progress.json missing** → payload reads refuse to read; `ReadProgressNodeFromSnapshot` is the nullable existence probe that returns null when the file is absent, and `LoadOrContinueStrict` turns that null into the actionable missing-`progress.json` error
- **Background level referenced by the topology has no payload** → refuse to load (consistent with the foreground: a topology referencing level data that does not exist must fail explicitly; silently mounting an empty session would hide data loss)
- **Payload level not referenced by the topology** → refuse to load (an unreferenced level would be silently ignored, losing its state; the topology and level set must describe the same sessions)
- **Format version marker** → written to `meta.map` whenever display metadata exists or the framework format is newer than v1; a no-metadata v1 save may omit it, and the reader maps the missing key to version 1.
- **Format version below 1** → refuse to load (the initial format is version 1; a no-metadata v1 save may omit the version key, so an explicit 0/negative value is corrupt data)
- **Format version above the current version** → refuse to load (the save was written by a newer framework)

## DataSourceNode Ownership Contract

- **Read methods** (`ReadSavePayloadFromSnapshot` / `ReadProgressNodeFromSnapshot` / `TryReadLevelPayload*` / `ResolveLevelPayload`) return payloads or nodes owned by the caller; the caller must dispose the contained `DataSourceNode` trees.
- **Write methods** (`WriteSavePayloadToCurrent*` / `WriteLevelPayloadOnlyToCurrent` / `WriteProgressOnlyToCurrent`) must fully consume the supplied node trees before returning; they must not retain references for later reads. The framework disposes nodes immediately after these write boundaries, so custom implementations must not access the nodes after returning either.
- Internal framework load/mount paths dispose payloads as soon as they are no longer needed; public-interface caller ownership is never managed on their behalf by internal framework paths.
- A strict read that fails after some files were decoded disposes every partial `DataSourceNode` tree before rethrowing, so a corrupt save cannot leak managed node trees even though no payload is returned.

## Deleting Save Slots

`ISaveStorageService.DeleteSave(string saveId)` removes `save_{id}/` and also cleans the matching `save_{id}.tmp/` remnant plus the policy-discovered backup path (including a case-variant or custom-name backup, and the standard `save_{id}.bak/` when it differs). The method never touches `current/`.

- The save ID is first validated as a lowercase, path-safe token (ASCII lowercase letters, digits, `.`, `_`, `-`; no dot-only value and no trailing `.`) and must not end in the reserved `.tmp`/`.bak` suffixes, matching the write/read path. Level IDs use the same identity rule because they name `level_{id}/`.
- When neither the slot, the temp directory, nor any backup directory exists, the method throws `InvalidOperationException` rather than silently succeeding.
- The `.tmp`/backup remnants are removed before the real slot. Recovery promotes a complete `.bak` when the real slot is missing, so deleting the real slot first would let a failed backup cleanup resurrect the deleted save on the next enumeration. With this order, a failed remnant cleanup leaves the real slot intact and there is no orphaned backup to recover. Two case-variant backups are both removed rather than leaving one to be recovered after the delete.
- Active-save protection belongs to the `ISndSaveOperations` business layer; the storage layer deletes exactly the slot it is given.

## Design Decisions

### Why slot deletion is implemented once in the storage layer

Deletion must cover the real slot and the `.tmp/.bak` remnants left by atomic writes. If UI or business code enumerated first and deleted paths itself, it would duplicate path-policy logic and could mistake `save_X.tmp` for an independent slot, breaking the recovery semantics of the next write. A single `DeleteSave` performs token validation and remnant cleanup in the storage layer, leaving active-slot and workflow protection to the business layer.

### Why save IDs reject the atomic-write suffixes

A save id is embedded directly in `save_{id}/`, while the snapshot phase uses `save_{id}.tmp/` and `save_{id}.bak/`. An id ending in `.tmp` or `.bak` would therefore describe a different valid slot and the same staging/backup path; saving or deleting one id could destroy the other. Validation rejects those suffixes (case-insensitively), and also enforces lowercase and path-safe IDs: default Windows/macOS file systems fold case, and Windows strips a segment's trailing period, so `Foo`/`foo` and `foo`/`foo.` would otherwise alias one directory. Legacy mixed-case slots are outside the supported id set and must be renamed before use; storage operations detect a case-variant legacy slot and fail explicitly instead of overwriting or deleting it.

### Why legacy level directories fail with a migration error

Level ids use the same lowercase, path-safe identity rule as save ids because they name `level_{id}/` directories. The writer deliberately preserves a legacy mixed-case or trailing-period level directory instead of deleting it, so its data is available for an explicit migration. The reader therefore cannot skip such a directory silently: the payload would be incomplete, and skipping it would hide the legacy data. It also must not expose the raw path-assembly `ArgumentException`, which reads like a programming error and does not tell the user what to do. Read discovery validates every discovered level-directory id first and fails with one `InvalidOperationException` that names the offending directories, explains the identity rule, and gives the rename example (`level_Town` → `level_town`) plus the topology update needed for a referenced level. The active level id from the persisted session topology is validated the same way before any path is assembled.

### Why interrupted snapshots are recovered

The backup-replace swap is safe against ordinary exceptions, but a process crash between `save_{id}/` → `save_{id}.bak/` and `save_{id}.tmp/` → `save_{id}/` leaves no real slot. Both enumeration and the snapshot read APIs that consume a saved slot (full payload, progress, and level reads) check for a complete backup and restore it as the real slot, so a direct load by ID recovers the same way as a list-then-load flow; incomplete backups and `.tmp` staging are ignored as slots. Directory-copy restore is not a recovery read and does not perform this check. This preserves the last known-good snapshot without presenting fake slots to the UI.

Recovery uses the path policy rather than assuming the default `save_`/`level_` names: `ISavePathPolicy.EnumerateInterruptedSnapshots` maps each save ID to the actual backup directory path, and completeness is checked through the policy's level paths. The recovery rename uses that actual path, so a custom layout can use a different slot prefix by overriding the enumeration and level-ID mapping instead of relying on the framework's default names.

### Why enumeration goes through ISavePathPolicy

`ISavePathPolicy.EnumerateSaveIds` gives every layout policy a single place to expose its slot IDs. `EnumerateInterruptedSnapshots` does the same for recovery candidates (ID plus actual backup path), and `TryGetLevelIdFromDirectory` supplies the inverse mapping used when discovering level payloads, pruning stale levels, and checking snapshot completeness, so write, list, metadata, delete, load, and recovery paths agree on the layout; an unreadable or invalid directory is skipped rather than breaking metadata listing for every valid slot; a legacy case-variant slot is not silently merged and is rejected by write/delete/load entry points with a rename instruction. Extra data restore and the idempotency side hash use `GetExtraDirectory` for the same reason.

### Why two-phase write

`current/` is written first to ensure data lands; the snapshot phase copies verified complete data to persistent slots. If the snapshot phase fails, `current/` still retains complete data (but the marker remains, causing the next read to reject it with a log notification). A situation of "snapshot slot has data but `current/` was lost due to crash" will never occur.

### Why not use temp file + rename for atomic single-file writes

Saves involve multiple files (progress.json + 3 files per N levels). Atomic rename of a single file cannot guarantee multi-file consistency. The `.write_in_progress` marker serves as a transaction marker for the entire save directory.

### Why ISavePathPolicy is replaceable

Different platforms (desktop, mobile, cloud save) may require different path layouts. Injecting the layout policy into `DefaultSaveStorageService` rather than hardcoding allows platform-specific policies to be injected at the adapter layer.

Directory and slot members (`GetCurrentDirectory`, `GetSaveDirectory`, `GetLevelDirectory`, …) may rename any directory segment. File-path members must keep the suffix that codec routing uses for their content type (`.json`, `.map`, `.sha`, `.write_in_progress`): every payload file is read and written through `IDataSourceIoGateway` suffix routing, so renaming `progress.json` to `progress.dat` or the payload SHA file to a non-`.sha` name makes the next write/read fail with an explicit "no codec configured" error instead of using the policy path.

### Why SaveFileHandle uniformly encapsulates I/O dependencies

`SaveFileHandle` encapsulates the five I/O dependencies `(IFileMetaAccess, IDataSourceIoGateway, IPathResolver, string saveRootPath, ISavePathPolicy)` into a single parameter object, making methods in SavePayloadReader/SavePayloadWriter/SaveStorageFacade map one-to-one with their implementations, avoiding the multi-level overload chains needed for passing the five-piece set layer by layer. `DefaultSaveStorageService` internally holds only a single field. It simultaneously carries path utility methods and gateway creation logic without needing separate helper classes.

### Why use SHA digest for idempotent deduplication

When the same game state is written to the same save slot multiple times, SHA-256 digest comparison avoids unnecessary I/O. At the `WriteSavePayloadToCurrentThenSnapshot` entry point, the target snapshot's `.payload.sha` is compared against the hash of the payload to be written:

- **Hash matches and `current/` has no `.write_in_progress` marker** → log INFO
  "idempotent save skip", return immediately, no file operations performed
- **Hash differs, `.sha` does not exist, or `current/` still has a marker** → proceed
  with the normal two-phase write flow

`ComputePayloadHash` computes SHA individually for each component's node tree and combines them, ensuring:
- Progress blackboard change → hash differs → rewrite
- Any level data change → hash differs → rewrite
- CustomMeta key-value change → hash differs → rewrite (map and level segments are length-prefixed, so values/keys containing `=` cannot make two distinct maps collide)
- Node text change → hash differs → rewrite; text that is not well-formed UTF-16 (an unpaired surrogate) is rejected while hashing, because a replacement-fallback encoder would collapse it into U+FFFD and could make two distinct trees hash-equal
- Only the target slot differs and the payload states no `SaveId` → always write (new slot has no .sha to compare). A non-empty payload `SaveId` that disagrees with the target (including an empty target) fails fast instead of silently writing another slot.

The `current/.payload.sha` writes follow three deliberate, non-conflicting semantics: the snapshot path (`WriteSavePayloadToCurrentThenSnapshot`) writes the **combined hash** (payload + the policy's extra directory), which the next idempotency comparison consumes; the test path (`SaveStorageAndPayloadTests.WritePayloadWithPayloadSha`, which calls `SavePayloadWriter.WriteToCurrent` then `SaveAtomicWriter.WritePayloadSha`) writes the **payload-only hash** (tests have no extra side channel, so a combined hash would be inaccurate); the load-recovery path (`DefaultSaveStorageService.WriteSavePayloadToCurrent`) writes **no hash** — a recovery write has no idempotency contract, and only the snapshot phase performs deduplication. The only consumer of `.payload.sha` is `TryIdempotentSkip` comparing against the snapshot directory; the `current/` hash itself is consumed only indirectly when it is copied into a snapshot.

### Why a mismatched payload save id fails fast

`SaveGamePayload.SaveId` records the slot a payload was built for, while `WriteSavePayloadToCurrentThenSnapshot` also takes the target slot id. The target id names `save_{id}/` and must be non-empty. When the payload states a non-empty `SaveId`, it must equal the target id; any disagreement means the payload would be persisted under a slot it does not claim to belong to, and silently preferring either identity would hide a caller bug. The write therefore fails fast with `ArgumentException` before any save file is written (the check runs before the hash/dedup step; the public default service's read-only case-variant slot probe still precedes it). A non-empty payload id with an empty target is rejected by the same rule, and an empty target cannot write a slot. An empty payload `SaveId` means "no identity stated" and the target id names the slot. Read methods populate `SaveId` with the slot that was read.

### Why cross-storage-root extra/ restore takes an explicit source service

`RestoreExtraFilesFromSnapshot(string saveId)` means "restore from this service's own snapshot into this service's current/". Initial-save payloads and extra/ live under the read-only `res://` initial storage root, while the runtime restore target is the writable `user://` runtime storage root. Reusing the same-service overload would resolve the source path under the destination service's current/ — ignoring the initial save's extra/ files and potentially copying stale runtime snapshot files into the current save. The interface therefore adds `RestoreExtraFilesFromSnapshot(ISaveStorageService sourceStorage, string saveId)`, where the destination service explicitly names the source service. The default implementation supports only a default source; custom service pairs must be supported by a custom destination implementation, and unknown pairs throw explicitly instead of silently falling back.

### Why DataSourceNode computes a Canonical Hash rather than a post-serialization hash

`DataSourceNode.ComputeSha256Hash()` builds a deterministic canonical string iteratively (cycle detection included) and then applies SHA-256. Unlike the codec serialization approach, the canonical string does not depend on the codec version or indentation configuration; map keys are sorted with `StringComparer.Ordinal`, ensuring the same data tree always produces the same hash. The canonical text is UTF-16 well-formedness checked and hashed with a strict UTF-8 encoder: an unpaired surrogate would otherwise be replaced by U+FFFD, letting two distinct trees share one digest.

---

[↑ Back to Save](../README.en.md)
