<!-- docsync-pair: Origo.TestSupport/Strategies/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Strategies

> [↑ Back to TestSupport](../README.en.md)

## Overview

Shared test strategy base classes and strategy index constants for all test projects. Provides reusable strategy implementations for common test behaviors: frame counting, blackboard read/write, entity peer lookup, deferred action probing, and more.

## File Inventory

| File | Responsibility |
|------|---------------|
| `SharedTestStrategies.cs` | Abstract strategy base classes: `SharedFrameCounterStrategy` (increments count data each frame), `SharedBlackboardReaderStrategy` (reads blackboard value into entity data), `SharedBlackboardWriterStrategy` (writes entity data to blackboard), `SharedKillOnProcessStrategy` (requests Kill on first Process), `SharedPeerLookupStrategy` (peer lookup via Session.FindByName), `SharedDeferredProbeStrategy` (verifies deferred actions via Deferred.EnqueueBusinessDeferred), `SharedConsoleCommandStrategy` (subscribes to console output). |
| `TestStrategyIndices.cs` | Static constant collection of all test strategy indices (`test.frame_counter`, `test.bb_reader`, `test.bb_writer`, etc.) with automatic duplicate detection. |

## Design Decisions

### Why test strategies use abstract base classes instead of interfaces

Test strategies need to read/write entity data and blackboard in hooks like `Process` and `AfterSpawn`. Abstract base classes provide default no-op implementations so tests only override the hooks they care about, simplifying test strategy authoring. Strategies are registered through `StrategyPool` as standard `LifecycleStrategyBase`.

---

[↑ Back to TestSupport](../README.en.md)
