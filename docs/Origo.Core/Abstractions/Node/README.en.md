<!-- docsync-pair: Origo.Core/Abstractions/Node/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Node (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: GodotAdapter/Snd](../../../Origo.GodotAdapter/Snd/README.en.md)

## Overview
Defines the abstract engine node operation interface system. Core triggers basic node behavior through `INodeHandle`, creates instances through `INodeFactory`, and manages recovery/export through `INodeHost` (internal).

## Included Files

| File | Responsibility |
|------|------|
| `INodeFactory.cs` | Create node instances by resource identifier |
| `INodeHandle.cs` | Abstract node handle: Name / Free / SetVisible |
| `INodeHost.cs` | internal: Node container behavior — recovery, reclamation, metadata export |

## Interface Details

### INodeFactory

| Member | Description |
|------|------|
| `Create(logicalName, resourceId)` | Create node and return handle |

### INodeHandle

| Member | Description |
|------|------|
| `Name` | Node logical name |
| `Free()` | Free node resources |
| `SetVisible(bool)` | Control node visibility |

### INodeHost (internal)

| Member | Description |
|------|------|
| `GetNode(name)` | Get node handle by name |
| `GetNodeNames()` | Enumerate mounted node names |
| `Recover(NodeMetaData)` | Recover node from metadata |
| `Release()` | Reclaim all nodes |
| `SerializeMetaData()` | Export node metadata |

## Design Decisions

### Why INodeHost is internal
The contract for internal node management by SND entities. Strategy code accesses nodes through `ISndEntity` (composes `ISndNodeAccess`), unaware of node container lifecycle.

### Why INodeHandle does not expose native node objects
Core operates through `INodeHandle` methods without holding engine-specific types. Native node access is through adapter-layer extension methods (`GetNativeNode()`), keeping Core isolated from engine types.

---
[↑ Back to Abstractions](../README.en.md)
