<!-- docsync-pair: Origo.TestSupport/Architecture/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Architecture

> [↑ Back to Origo.TestSupport](../README.en.md)

## Overview

Architecture guardrail helpers for the test suites.

## Files

| File | Responsibility |
|------|----------------|
| `PrivateFieldNamingConvention.cs` | Reflectively verifies production private fields follow `_camelCase` naming |
| `Metadata/TypedDataTestSupport.cs` | Internal test reset helper: clears the TypedData kind registry and replays Home registration; production code has no test hook |

## Design Decisions

### Why naming rules use reflective tests instead of dotnet format

The private-field naming rule in `.editorconfig` is fix-only in `dotnet format --verify-no-changes`, so it cannot act as a failing gate. Architecture tests scan production-assembly private fields reflectively and make naming violations part of the normal test gate.

### Why TypedData reset lives in the test assembly

The global kind registry is process-wide static state that tests must reset between cases; however AGENTS §1.2 forbids production code from exposing test-convenience hooks. `Origo.TestSupport` uses `InternalsVisibleTo` to reach the internal registry and centralizes the reset, so production `TypedData` has no test-only API.

---
[↑ Back to Origo.TestSupport](../README.en.md)
