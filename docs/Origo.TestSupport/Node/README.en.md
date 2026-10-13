
# Node

> [↑ Back to TestSupport](../README.en.md)

## Overview

Test doubles for SND node abstractions: `INodeHandle` and `INodeFactory`, with invocation counting and simulated failure support.

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestNodeHandle.cs` | Implements `INodeHandle`. Exposes `FreeCount`, `IsVisible` state tracking, and `Name`. |
| `TestNodeFactory.cs` | Implements `INodeFactory`. Accepts an optional `IEnumerable<string>` of resource IDs to simulate creation failures. Records all successfully created `TestNodeHandle` instances (`CreatedHandles`) in creation order. |

## Design Decisions

### Why Node doubles are separate from SndSceneHost doubles

Node lifecycle (create → query → release) and scene host lifecycle (entity container management) are orthogonal concerns. Separate doubles allow tests to use only node mocks without pulling in scene host dependencies.

---

[↑ Back to TestSupport](../README.en.md)
