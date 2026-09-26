<!-- docsync-pair: Origo.TestSupport/Node/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->

# Node

> [↑ Back to TestSupport](../README.en.md)

## Overview

Test doubles for SND node abstractions: `INodeHandle` and `INodeFactory`, with invocation counting and simulated failure support.

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestNodeHandle.cs` | Implements `INodeHandle`. Exposes `FreeCount`, `IsVisible` state tracking, and `Name`. |
| `TestNodeFactory.cs` | Implements `INodeFactory`. Accepts an optional `IEnumerable<string>` of resource IDs to simulate creation failures. Records all created `TestNodeHandle` instances (`CreatedHandles`) and a list of creation request records (`Requests`); creation failures are simulated via the constructor-injected resource ID list. |

## Design Decisions

### Why Node doubles are separate from SndSceneHost doubles

Node lifecycle (create → query → release) and scene host lifecycle (entity container management) are orthogonal concerns. Separate doubles allow tests to use only node mocks without pulling in scene host dependencies.

---

[↑ Back to TestSupport](../README.en.md)
