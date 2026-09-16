<!-- docsync-pair: Origo.TestSupport/Architecture/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Architecture

> [↑ Back to Origo.TestSupport](../README.en.md)

## Overview

Architecture guardrail helpers for the test suites.

## Files

| File | Responsibility |
|------|----------------|
| `PrivateFieldNamingConvention.cs` | Reflectively verifies production private fields follow `_camelCase` naming |
| `Metadata/TypedDataTestSupport.cs` | Internal test reset helper: clears the TypedData kind registry and replays Home registration; production code has no test hook |
| `Runtime/SndContextTestFrameDriver.cs` | Internal test-side frame-flush extension: drains the runtime deferred queue only, without processing entities or pumping the console; production code has no test hook |

## Design Decisions

### Why naming rules use reflective tests instead of dotnet format

The private-field naming rule in `.editorconfig` is fix-only in `dotnet format --verify-no-changes`, so it cannot act as a failing gate. Architecture tests scan production-assembly private fields reflectively and make naming violations part of the normal test gate.

### Why TypedData reset lives in the test assembly

The global kind registry is process-wide static state that tests must reset between cases; however AGENTS §1.2 forbids production code from exposing test-convenience hooks. `Origo.TestSupport` uses `InternalsVisibleTo` to reach the internal registry and centralizes the reset, so production `TypedData` has no test-only API.

### Why test frame flushing goes through TestSupport instead of a production API

The frame boundary is Core's single access path (`IOrigoFrameDriver.DriveFrame`); production APIs do not expose the half-step of flushing only the deferred queue. SndContext workflow tests need to verify "enqueue → flush → effect" without also processing entities and executing commands, so `SndContextTestFrameDriver.FlushFrame` calls `OrigoRuntime.FlushEndOfFrameDeferred` through `InternalsVisibleTo`; production `ISndDeferredActions` stays sealed.

---
[↑ Back to Origo.TestSupport](../README.en.md)
