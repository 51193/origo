<!-- docsync-pair: Origo.Core.Contracts/Abstractions/Node/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Node (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: GodotAdapter/Snd](../../../Origo.GodotAdapter/Snd/README.en.md)

## Overview

Defines the abstract engine-node contracts between Core and adapter layers.
Core triggers basic node behavior (visibility, freeing) through `INodeHandle`
and creates node instances through `INodeFactory`; neither exposes concrete
engine node types.

The node container interface `INodeHost` is a Core-internal orchestration
contract and remains in
[Origo.Core.Kernel/Abstractions/Node](../../../Origo.Core.Kernel/Abstractions/Node/README.en.md).

## Included Files

| File | Responsibility |
|------|----------------|
| `INodeFactory.cs` | Create node instances by resource identifier |
| `INodeHandle.cs` | Abstract node handle: Name / Free / SetVisible |

## Interface Details

### INodeFactory

| Member | Description |
|------|-------------|
| `Create(logicalName, resourceId)` | Create node and return handle |

### INodeHandle

| Member | Description |
|------|-------------|
| `Name` | Node logical name |
| `Free()` | Free node resources |
| `SetVisible(bool)` | Control node visibility |

## Design Decisions

### Why INodeHandle does not expose native node objects

Core operates on nodes through `INodeHandle` methods and neither holds nor
exposes engine-specific types. When a native node is needed, the adapter's
`SndEntityNodeExtensions` provides `GetNativeNode()` and
`GetNodeFromSnd<T>()`; engine access is uniformly declared through those
adapter extensions, and `INodeHandle` itself exposes no engine type through
`object`.

---
[↑ Back to Abstractions](../README.en.md)
