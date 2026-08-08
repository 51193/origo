<!-- docsync-pair: Origo.Core.Tests/Save-Storage -->
<!-- docsync-revision: 9 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Persistence: Storage Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Save/Storage](../Origo.Core/Save/Storage/README.en.md)
> [↔ Behavior under test: usage/persistence-flow](../usage/persistence-flow.en.md)

## Behavior Overview

Validates the storage layer contract of the Origo persistence system: "strict reads, explicit failures, two-phase writes."
Covers `.write_in_progress` marker, level three-file-set integrity, missing `progress.json`,
snapshot creation/read round-trip, path policy customization, idempotent deduplication, Payload model defaults,
WellKnownKeys constants, SaveFileHandle path resolution, and traversal protection.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `SaveStorageContractTests.cs` | Storage contract: marker, three-file-set, two-phase write, snapshot, meta.map, path policy |
| `SaveStorageAndPayloadTests.cs` | Storage and Payload integration: write/read round-trip, LevelPayload write/read, snapshot atomicity, error recovery |
| `SaveIdempotencyTests.cs` | Idempotent deduplication: Payload SHA calculation, same/different payload write/skip, SHA file management |
| `SavePathLayoutTests.cs` | Path layout: directory file path assembly under default policy and invalid parameter guards |
| `SavePathPolicyContractTests.cs` | Path policy interface contract: ISavePathPolicy injection and policy-aware verification of all storage methods |
| `SavePathResolverTests.cs` | Path resolution: SaveFileHandle relative path extraction, parent directory creation, traversal attack rejection, leaf directory name |
| `SaveGamePayloadTests.cs` | Data model: SaveGamePayload/LevelPayload defaults, multi-level access, CustomMeta |
| `WellKnownKeysTests.cs` | Constants: ActiveSaveId, SessionTopology key name correctness |
| `SaveIdValidationTests.cs` | Save id validation: `RequestSaveGame`/`RequestLoadGame`/`SetContinueTarget` reject invalid ids (path separators / out-of-range chars), accept valid ids |
| `SaveExtraFilesRoundTripTests.cs` | extra/ side-channel files: snapshot-to-current copy round-trip, structure preservation, missing/empty dir tolerance, argument validation |
| `SaveFormatVersionTests.cs` | Save format version: origo.format_version written to meta.map, newer versions rejected on load, missing version key tolerated, reserved keys hidden |
| `SaveSnapshotMarkerTests.cs` | Snapshot integrity: no .write_in_progress residue in snapshot directory |
| `StaleLevelDirectoryCleanupTests.cs` | Regression: after a full save `current/` is consistent with the payload's level set — level directories of destroyed background sessions are cleaned up, not leaked into subsequent snapshots |

## SaveStorageContractTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `WriteSaveToCurrent_CreatesMarkerDuringWrite` | After WriteToCurrent, progress.json exists, marker deleted | persistence-flow: Phase 1 |
| `ReadSavePayloadFromCurrent_WhenNoMarker_Succeeds` | Read Payload normally without marker | persistence-flow: Strict Reads |
| `WriteSavePayloadToCurrent_WritesAllExpectedFiles` | After write, current/ contains all expected files | persistence-flow: File Layout |
| `WriteSavePayloadToCurrentThenSnapshot_CreatesSnapshotDirectory` | Snapshot phase creates save_x/ directory with contents | persistence-flow: Phase 2 |
| `WriteSavePayloadToCurrentThenSnapshot_ThenReadBackRoundTrip` | Snapshot → read round-trip data consistent | persistence-flow |
| `SnapshotCurrentToSave_WritesAllLevelFiles` | SnapshotCurrentToSave writes all level files | persistence-flow: Phase 2 |
| `WriteSavePayloadToCurrent_ValidPayload_WritesSuccessfully` | Valid Payload correctly written to current/ | SaveGamePayload model |
| `WriteSavePayloadToCurrent_EmptySaveId_StillWrites` | Writes even with empty SaveId | SaveGamePayload model |
| `WriteSavePayloadToCurrentThenSnapshot_WithCustomMeta_WritesMetaMap` | CustomMeta non-empty writes meta.map | persistence-flow: meta.map |
| `WriteSavePayloadToCurrentThenSnapshot_WithoutCustomMeta_MetaMapNotCreated` | CustomMeta null does not create meta.map | persistence-flow: meta.map |
| `DefaultSaveStorageService_WithCustomPathPolicy_UsesCustomLayout` | ISavePathPolicy custom path layout effective | ISavePathPolicy |
| `EnumerateSaveIds_ReturnsCorrectList` | Enumerated saves do not include current directory | ISaveStorageService |
| `DeleteCurrentDirectory_RemovesAllCurrentFiles` | DeleteCurrentDirectory removes all current/ contents | ISaveStorageService |
| `TryReadLevelPayload_AllThreePresent_Succeeds` | All three level files present returns full LevelPayload | persistence-flow: Strict Reads |
| `WriteProgressOnlyToCurrent_RemovesMarkerOnSuccess` | Checkpoint write succeeds with no marker residue, files exist | persistence-flow: Two-Phase Write |
| `WriteLevelPayloadOnlyToCurrent_RemovesMarkerOnSuccess` | Level-only write succeeds with no marker, readable back | persistence-flow: Two-Phase Write |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `ReadSavePayloadFromCurrent_WhenWriteInProgressMarkerExists_Throws` | .write_in_progress file exists in current/ | InvalidOperationException |
| `TryReadLevelPayload_OnlySndSceneExists_Throws` | Level only has snd_scene.json | InvalidOperationException (data corruption) |
| `TryReadLevelPayload_OnlySessionExists_Throws` | Level only has session.json | InvalidOperationException (data corruption) |
| `TryReadLevelPayload_OnlyStateMachinesExists_Throws` | Level only has session_state_machines.json | InvalidOperationException (data corruption) |
| `TryReadLevelPayload_AnyTwoOfThree_Throws` | Any two of the three level files present | InvalidOperationException (data corruption) |
| `ReadSavePayloadFromCurrent_WhenProgressJsonMissing_Throws` | progress.json missing | Throws exception |
| `ReadSavePayloadFromSnapshot_WhenSaveNotExist_Throws` | Non-existent save snapshot | InvalidOperationException |
| `WriteProgressOnlyToCurrent_Failure_LeavesMarkerSoReadersReject` | I/O failure mid checkpoint write (simulated second write throws) | IOException propagates, marker left so readers reject |
| `WriteLevelPayloadOnlyToCurrent_Failure_LeavesMarkerSoReadersReject` | Level-only write validation fails (Null nodes) | InvalidOperationException, marker left so readers reject |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `TryReadLevelPayload_AllThreeMissing_ReturnsNull` | All three level files missing | Returns null (no save) |
| `StaleWriteMarker_AfterDeleteCurrentDirectory_WriteThenSucceeds` | stale marker → DeleteCurrentDirectory → rewrite | New data writable and readable |
| `RecoverFromStaleWriteMarker_CleanStateAfterRecovery` | Clean current/ state after recovery | No marker residue, data normal |
| `DeleteCurrentDirectory_WhenNoDirectory_DoesNotThrow` | Delete when current/ does not exist | Does not throw (idempotent) |

## SaveFormatVersionTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Save_WritesFormatVersionToMetaMap` | Save writes `origo.format_version: 1` to meta.map | persistence-flow: meta.map |
| `ListSaves_HidesFrameworkReservedMetaKeys` | ListSaves/EnumerateSavesWithMetaData hide `origo.*` framework-reserved keys | persistence-flow: meta.map |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `Load_RejectsSaveWithNewerFormatVersion` | Save format version newer than current (99) | InvalidOperationException (load rejected) |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Load_AcceptsMissingFormatVersionKey` | Old save meta.map without version key | Treated as version 1, loads normally |

## SaveSnapshotMarkerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Snapshot_DoesNotContainWriteInProgressMarker` | Snapshot directory has no `.write_in_progress` file after a full save | persistence-flow: Two-Phase Write |

## StaleLevelDirectoryCleanupTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SaveAfterDestroyingBackgroundSession_PrunesStaleLevelDirectory` | After saving foreground + background sessions each with one level, destroy the background and save again: neither `current/` nor the new snapshot contains the destroyed session's level directory; surviving levels are retained | Save/Storage: stale level directory cleanup |

## SaveStorageAndPayloadTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SaveStorageFacade_WriteAndReadCurrent_RoundTrip` | WriteToCurrent → ReadFromCurrent round-trip data consistent | ISaveStorageService |
| `SaveStorageFacade_ReadProgressNodeFromSnapshot_WhenPresent_ReturnsContent` | Read back progress node content when snapshot exists | ISaveStorageService |
| `SaveStorageFacade_EnumerateSavesWithMetaData_SlotWithoutMetaMap_StillListed` | Save slot without meta.map still listed | ISaveStorageService |
| `SaveStorageFacade_SnapshotCurrentToSave_AndEnumerateSaveIds_Works` | WriteToCurrent → Snapshot → Enumerate full flow | ISaveStorageService |
| `SaveStorageFacade_SnapshotCurrentToSave_UsesTempDirectoryThenRename` | Snapshot uses .tmp directory then rename, no residue | persistence-flow: Phase 2 |
| `SnapshotCurrentToSave_OverwritingExistingSave_ReplacesContentAndLeavesNoBackup` | Overwriting existing save replaces content, no .bak/.tmp residue | ISaveStorageService |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SaveStorageFacade_EnumerateSaveIds_NullFileSystem_Throws` | null IFileSystem | ArgumentNullException |
| `SaveStorageFacade_SnapshotCurrentToSave_WhitespaceSaveRoot_Throws` | Whitespace storage root path | ArgumentException |
| `SaveStorageFacade_SnapshotCurrentToSave_WhitespaceNewSaveId_Throws` | Whitespace save ID | ArgumentException |
| `SaveStorageFacade_ReadSavePayloadFromSnapshot_WhitespaceSaveRoot_Throws` | Whitespace storage root path | ArgumentException |
| `SaveStorageFacade_ReadCurrent_MissingProgressStateMachines_Throws` | progress_state_machines.json missing | InvalidOperationException |
| `SaveStorageFacade_ReadCurrent_MissingSessionStateMachines_Throws` | session_state_machines.json missing | InvalidOperationException |
| `WriteToCurrent_WhenActiveLevelMissing_ThrowsWithoutWritingCurrent` | LevelPayload for ActiveLevelId missing | InvalidOperationException, no residue in current/ |
| `SaveStorageFacade_ReadCurrent_ActiveLevelPartial_MissingSession_Throws` | Active level missing session.json | InvalidOperationException |
| `SaveStorageFacade_ReadCurrent_BackgroundLevelPartial_MissingStateMachines_Throws` | Background level missing session_state_machines.json | InvalidOperationException |
| `SaveStorageFacade_ReadCurrent_WhenWriteMarkerExists_Throws` | .write_in_progress exists in current/ | InvalidOperationException |
| `SavePayloadReader_TryReadLevelPayloadFromCurrent_WhenWriteMarkerExists_Throws` | .write_in_progress exists when reading level | InvalidOperationException |
| `DefaultSaveStorageService_ResolveLevelPayload_WhenWriteMarkerExists_Throws` | .write_in_progress exists during ResolveLevelPayload | InvalidOperationException |
| `WriteSavePayloadToCurrentThenSnapshot_NullLogger_Throws` | null logger | ArgumentNullException |
| `WriteSavePayloadToCurrentThenSnapshot_WhenSnapshotFails_LogsError_LeavesMarkerAndUpdatedCurrent` | Copy fails during snapshot phase | InvalidOperationException, current/ remains in written state, marker residue |
| `SaveStorageFacade_SnapshotCurrentToSave_CleansUpTempOnFailure` | Snapshot Copy fails | .tmp directory cleaned up |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SaveStorageFacade_ReadProgressNodeFromSnapshot_Missing_ReturnsNull` | Snapshot directory does not exist | Returns null |
| `SavePayloadReader_TryReadLevelPayloadFromCurrent_AllFilesAbsent_ReturnsNull` | All level files missing | Returns null |

## SaveIdempotencyTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `ComputePayloadHash_SamePayload_SameHash` | Same Payload produces same 64-char hex SHA | SavePayloadWriter |
| `ComputePayloadHash_DifferentProgressNode_DifferentHash` | ProgressNode change causes Hash change | SavePayloadWriter |
| `ComputePayloadHash_DifferentLevelContent_DifferentHash` | Level SessionNode change causes Hash change | SavePayloadWriter |
| `ComputePayloadHash_DifferentCustomMeta_DifferentHash` | CustomMeta value change causes Hash change | SavePayloadWriter |
| `ComputePayloadHash_CustomMetaOrder_Independent` | CustomMeta key order does not affect Hash | SavePayloadWriter |
| `ComputePayloadHash_LevelOrder_Independent` | Levels dictionary key order does not affect Hash | SavePayloadWriter |
| `WriteToCurrent_CreatesPayloadShaFile` | WriteToCurrent writes .payload.sha file | SavePayloadWriter |
| `WriteSavePayloadToCurrentThenSnapshot_SamePayloadTwice_SecondSkips` | Same Payload second write skipped, current/ not rebuilt | persistence-flow: Idempotent |
| `WriteSavePayloadToCurrentThenSnapshot_DifferentPayload_Overwrites` | Different Payload overwrites normally | persistence-flow: Idempotent |
| `WriteSavePayloadToCurrentThenSnapshot_NewSaveId_AlwaysWrites` | New SaveId always writes (no existing SHA to compare) | persistence-flow |
| `WriteSavePayloadToCurrentThenSnapshot_ExistingSaveNoSha_WritesAndCreatesSha` | Existing snapshot without .payload.sha writes and creates SHA | persistence-flow |
| `WriteSavePayloadToCurrentThenSnapshot_CorruptedShaFile_WritesAndOverwrites` | Existing snapshot .payload.sha corrupted, overwrites with correct SHA | persistence-flow |
| `WriteSavePayloadToCurrentThenSnapshot_WhenWriteMarkerExists_StillThrows` | Even with marker present, hash mismatch completes normally | persistence-flow: Idempotent |
| `SnapshotCurrentToSave_CopiesPayloadShaFile` | SnapshotCurrentToSave copies .payload.sha to snapshot | SaveStorageFacade |
| `WriteSavePayloadToCurrentThenSnapshot_IdempotentSkip_PreservesExistingSnapshot` | On idempotent skip, snapshot content unchanged | persistence-flow: Idempotent |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `WriteSavePayloadToCurrentThenSnapshot_ShaReadError_PropagatesException` | SHA file read fails | InvalidOperationException (propagates original exception) |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `ComputePayloadHash_EmptyPayload_Works` | Payload with all empty nodes | Returns valid 64-char hex SHA |
| `ComputePayloadHash_NullCustomMeta_DoesNotThrow` | CustomMeta is null | Does not throw, computes Hash normally |

## SavePathLayoutTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SavePathLayout_GetCurrentDirectory_ReturnsCurrent` | Returns "current" | SavePathLayout |
| `SavePathLayout_CurrentDirectoryName_Constant` | Constant value is "current" | SavePathLayout |
| `SavePathLayout_GetSaveDirectory_FormatsCorrectly` | saveId → "save_{id}" format | SavePathLayout |
| `SavePathLayout_GetProgressFile_CombinesCorrectly` | base → "base/progress.json" | SavePathLayout |
| `SavePathLayout_GetProgressStateMachinesFile_CombinesCorrectly` | base → "base/progress_state_machines.json" | SavePathLayout |
| `SavePathLayout_GetCustomMetaFile_CombinesCorrectly` | base → "base/meta.map" | SavePathLayout |
| `SavePathLayout_GetLevelDirectory_CombinesCorrectly` | base + levelId → "base/level_{levelId}" | SavePathLayout |
| `SavePathLayout_GetLevelSndSceneFile_CombinesCorrectly` | levelDir → "level_dir/snd_scene.json" | SavePathLayout |
| `SavePathLayout_GetLevelSessionFile_CombinesCorrectly` | levelDir → "level_dir/session.json" | SavePathLayout |
| `SavePathLayout_GetLevelSessionStateMachinesFile_CombinesCorrectly` | levelDir → "level_dir/session_state_machines.json" | SavePathLayout |
| `SavePathLayout_GetWriteInProgressMarker_CombinesCorrectly` | base → "base/.write_in_progress" | SavePathLayout |
| `SavePathLayout_WriteInProgressMarkerName_Constant` | Constant value is ".write_in_progress" | SavePathLayout |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SavePathLayout_GetSaveDirectory_ThrowsOnInvalidId` | null / empty / whitespace saveId | ArgumentException |
| `SavePathLayout_GetProgressFile_ThrowsOnEmpty` | Empty base directory | ArgumentException |
| `SavePathLayout_GetProgressStateMachinesFile_ThrowsOnWhitespace` | Whitespace base directory | ArgumentException |
| `SavePathLayout_GetCustomMetaFile_ThrowsOnNull` | null base directory | ArgumentException |
| `SavePathLayout_GetLevelDirectory_ThrowsOnInvalidArgs` | Empty/whitespace base or levelId | ArgumentException |
| `SavePathLayout_GetLevelSndSceneFile_ThrowsOnEmpty` | Empty level directory | ArgumentException |
| `SavePathLayout_GetLevelSessionFile_ThrowsOnWhitespace` | Whitespace level directory | ArgumentException |
| `SavePathLayout_GetLevelSessionStateMachinesFile_ThrowsOnNull` | null level directory | ArgumentException |
| `SavePathLayout_GetWriteInProgressMarker_ThrowsOnEmpty` | Empty base directory | ArgumentException |

## SavePathPolicyContractTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SndContext_DefaultStorage_Uses_Injected_SavePathPolicy` | SndContext default storage uses injected ISavePathPolicy | ISavePathPolicy |
| `SndContext_DefaultInitialStorage_Uses_Injected_SavePathPolicy` | SndContext initial storage uses injected ISavePathPolicy | ISavePathPolicy |
| `SystemRuntime_DefaultStorage_Uses_Injected_SavePathPolicy` | SystemRuntime default storage uses injected ISavePathPolicy | ISavePathPolicy |
| `DefaultSaveStorageService_EnumerateSaveIds_Uses_PathPolicy` | EnumerateSaveIds assembles paths through policy | ISavePathPolicy |
| `DefaultSaveStorageService_EnumerateSavesWithMetaData_Uses_PathPolicy` | EnumerateSavesWithMetaData reads meta.map through policy | ISavePathPolicy |
| `DefaultSaveStorageService_WriteSavePayloadToCurrentThenSnapshot_Uses_PathPolicy` | Two-phase write fully passes through policy-assembled paths | ISavePathPolicy |
| `DefaultSaveStorageService_SnapshotCurrentToSave_Uses_PathPolicy` | SnapshotCurrentToSave assembles snapshot path through policy | ISavePathPolicy |
| `DefaultSaveStorageService_WriteSavePayloadToCurrent_Uses_PathPolicy` | WriteToCurrent assembles file paths through policy | ISavePathPolicy |
| `DefaultSaveStorageService_ReadWriteRoundTrip_Uses_PathPolicy` | Write→Read round-trip under policy-customized paths | ISavePathPolicy |
| `SessionStateMachineContext_SceneAccess_PointsToForegroundSession_ForegroundAndBackground` | In foreground/background Session state machines, SceneAccess each points to own SceneHost | StateMachineStrategyBase |
| `SessionStateMachineContext_SessionBlackboard_PointsToForegroundSession_ForegroundAndBackground` | In foreground/background Session state machines, SessionBlackboard each isolated | StateMachineStrategyBase |

## SaveFileHandleTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `SavePathResolver_EnsureParentDirectory_CreatesParent` | Auto-creates parent directory when it does not exist | SaveFileHandle |
| `SavePathResolver_GetRelativePath_ExtractsRelative` | Extracts relative portion from absolute path | SaveFileHandle |
| `SavePathResolver_GetRelativePath_NestedPath` | Multi-level nested path extracts relative portion | SaveFileHandle |
| `SavePathResolver_GetLeafDirectoryName_ReturnsLastSegment` | Multi-segment path returns last segment | SaveFileHandle |
| `SavePathResolver_GetLeafDirectoryName_SingleSegment` | Single-segment path returns itself | SaveFileHandle |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `SavePathResolver_GetRelativePath_RejectsTraversalInRelativeSegment` | Path contains `../` traversal | ArgumentException |
| `SavePathResolver_GetRelativePath_WhitespaceRoot_ThrowsOnConstruction` | Empty/whitespace storage root path construction | ArgumentException |
| `SavePathResolver_RejectPathTraversal_ThrowsOnDotDot` | Input contains `..` variants | ArgumentException |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `SavePathResolver_EnsureParentDirectory_NoOpForRootFile` | File at root level, no parent directory creation needed | Does not throw |
| `SavePathResolver_GetRelativePath_ExactMatch_ReturnsEmpty` | Path exactly equals storage root | Returns empty string |
| `SavePathResolver_GetRelativePath_NoMatch_ReturnsFullPath` | Path not under storage root | Returns full absolute path |
| `SavePathResolver_GetLeafDirectoryName_TrailingSlash` | Path with trailing slash | Returns last segment name |
| `SavePathResolver_GetLeafDirectoryName_EmptyOrWhitespace_ReturnsEmpty` | Empty/whitespace path | Returns empty string |
| `SavePathResolver_RejectPathTraversal_AllowsSafePaths` | Safe path (no `..`) | Does not throw |

## SaveGamePayloadTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `CurrentFormatVersion_IsOne` | FormatVersion constant value is 1 | SaveGamePayload |
| `WithSingleLevel_CanAccessLevel` | Single-level Payload accessible via Levels dictionary | SaveGamePayload |
| `WithMultipleLevels_AllAccessible` | Multi-level Payload all accessible | SaveGamePayload |
| `CustomMeta_CanBeSet` | CustomMeta dictionary settable and readable | SaveGamePayload |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `DefaultValues` | SaveGamePayload default construction | SaveId/ActiveLevelId are empty strings, ProgressNode IsNull, Levels non-null |
| `LevelPayload_DefaultValues` | LevelPayload default construction | LevelId is empty string, all Nodes IsNull |
| `CustomMeta_Null_Allowed` | CustomMeta set to null | null allowed |

## WellKnownKeysTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `WellKnownKeys_ActiveSaveId_HasExpectedValue` | ActiveSaveId = "origo.active_save_id" | WellKnownKeys |
| `WellKnownKeys_SessionTopology_HasExpectedValue` | SessionTopology = "origo.session_topology" | WellKnownKeys |

## SaveExtraFilesRoundTripTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `CopyDirectoryFromSnapshot_SeededFiles_AllCopiedToCurrent` | All seeded files under snapshot save_001/extra are copied to current/extra | SaveStorageFacade.CopyDirectoryFromSnapshot |
| `CopyDirectoryFromSnapshot_SubdirectoryStructurePreserved` | Subdirectory hierarchy preserved after copy | SaveStorageFacade.CopyDirectoryFromSnapshot |
| `CopyDirectoryFromSnapshot_ExistingFilesInCurrent_Overwrites` | Existing same-name files in current are overwritten by snapshot content | SaveStorageFacade.CopyDirectoryFromSnapshot |
| `ExtraFiles_FullSaveLoadRoundTrip_PreservesMultipleFiles` | Multiple extra files survive save→load round-trip with content and structure | persistence-flow: extra |
| `ExtraFiles_SaveLoadRoundTrip_SubdirectoryPreserved` | Extra files in subdirectories survive round-trip | persistence-flow: extra |
| `ExtraFiles_SaveTwice_SameSlot_HasLatestContent` | Saving twice to the same slot, load yields latest content | persistence-flow: extra |
| `ExtraFiles_SaveLoadRoundTrip_TypeDataRoundTrip_PreservesNumbers` | TypedData write/read (int/bool/string) round-trip preserves values | persistence-flow: extra |
| `ExtraFiles_DeleteFileThenSave_FileNotInSnapshot` | Deleted file is not in snapshot after save | persistence-flow: extra |
| `ExtraFiles_DifferentContent_DifferentCombinedHash` | Extra content change changes .payload.sha hash | persistence-flow: Idempotent |
| `ExtraFiles_LoadWithoutExtra_DoesNotThrowAndPreviousStateCleared` | Loading a save without extra directory does not throw | persistence-flow: extra |
| `IdempotentSkip_UnchangedPayloadAndExtra_SkipHappens` | Second save skipped idempotently with log when payload and extra hashes unchanged | persistence-flow: Idempotent |
| `CombineHashes_EmptySide_ProducesConsistentFormat` | Combining with an empty side still yields 64-char hex | SaveAtomicWriter |
| `CombineHashes_SamePayload_EmptyAndNonEmptySide_DifferentResult` | Same payload hash, empty side vs non-empty side produce different results | SaveAtomicWriter |
| `CombineHashes_WithExtra_DifferentFromPayloadHash` | Combined hash with extra differs from pure payload hash | SaveAtomicWriter |
| `ComputeSideDirectoryHash_WithFiles_ReturnsNonEmpty` | Directory with files returns non-empty 64-char hex hash | SaveAtomicWriter |
| `ComputeSideDirectoryHash_SameContent_SameHash` | Same content computed twice yields same hash | SaveAtomicWriter |
| `ComputeSideDirectoryHash_DifferentContent_DifferentHash` | Content change yields different hash | SaveAtomicWriter |
| `ComputeSideDirectoryHash_CustomDirectoryName_Works` | Custom directory name also produces a 64-char hex hash | SaveAtomicWriter |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `CopyDirectoryFromSnapshot_SourceDirectoryDoesNotExist_ReturnsSilently` | No extra/ directory in snapshot | Returns silently, no exception |
| `CopyDirectoryFromSnapshot_EmptySourceDirectory_DoesNothing` | extra/ is an empty directory | current/extra created but stays empty |
| `ComputeSideDirectoryHash_NoExtraDir_ReturnsEmpty` | No extra/ directory | Returns empty string |
| `ComputeSideDirectoryHash_EmptyExtraDir_ReturnsEmpty` | extra/ directory exists but is empty | Returns empty string |
| `ComputeSideDirectoryHash_CustomDirectory_Empty_ReturnsEmpty` | Custom directory is empty | Returns empty string |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `CopyDirectoryFromSnapshot_NullHandle_Throws` | handle is null | ArgumentNullException |
| `CopyDirectoryFromSnapshot_EmptySaveId_Throws` | saveId is empty string | ArgumentException |
| `CopyDirectoryFromSnapshot_EmptyDirName_Throws` | relativeDirName is empty string | ArgumentException |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `CustomSavePathPolicy` | SaveStorageContractTests.cs | Custom ISavePathPolicy, all methods generate prefixed test paths |
| `FailOnCopyFileSystem` | SaveStorageAndPayloadTests.cs | Throws exception when Copy target matches substring, simulates snapshot copy failure |
| `ThrowingOnReadFileSystem` | SaveIdempotencyTests.cs | Throws exception when ReadAllText accesses specified path, simulates SHA file read failure |
| `TestPrefixedPathPolicy` | SavePathPolicyContractTests.cs | Prefixed custom ISavePathPolicy, verifying policy injection through all storage methods |
| `SceneContractStrategy` | SavePathPolicyContractTests.cs | State machine strategy, collects SceneHost entity names in OnPushRuntime |
| `BbContractStrategy` | SavePathPolicyContractTests.cs | State machine strategy, collects SessionBlackboard values in OnPushRuntime |
| `NoOpPopContractStrategy` | SavePathPolicyContractTests.cs | Empty state machine strategy implementation for contract test state machine Push |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| Atomicity of rename failure in Phase 2 | .tmp residue cleanup during snapshot | persistence-flow: Phase 2 description |
| Queuing behavior of concurrent requests during SaveStorageFacade write | Concurrency safety | — |
| Performance of snapshot with many level files (deep directory tree recursive copy) | I/O behavior under extreme scenarios | — |

## Design Decisions

### Why use TestMemoryFileSystem instead of the real file system

Per the documentation: all file operations in the Core layer go through `IFileSystem`; direct `File.*` API is forbidden.
Therefore tests should not depend on the real file system — this would break the Core layer's platform independence.

### Why not test DefaultSaveStorageService internal implementation details

`DefaultSaveStorageService` is an internal type. Tests verify behavior through the `ISaveStorageService` interface injected into SndContext, rather than directly testing internal implementation.

### Why separate SaveStorageContractTests is needed

The original persistence tests were scattered across `LifecycleRunsTests`, `DisposeSemanticsTests`,
and `SndContextWorkflowTests`. `SaveStorageContractTests` consolidates the 7 strict-read rules described in the documentation, making contract verification an independent, auditable test unit.

---

[↑ Back to Origo.Core.Tests](README.en.md)
