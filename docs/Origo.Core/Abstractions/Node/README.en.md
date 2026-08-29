<!-- docsync-pair: Origo.Core/Abstractions/Node/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Node (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: GodotAdapter/Snd](../../../Origo.GodotAdapter/Snd/README.en.md)

## Overview
Defines the abstract engine node operation interface system. Core triggers basic node behavior (visibility, freeing) through `INodeHandle`, creates node instances through `INodeFactory`, and manages node recovery and export through `INodeHost` (internal). None of the interfaces expose concrete engine types — `INodeHandle` contains no reference to any engine-native node.

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
`INodeHost` is the contract for SND entities' internal node management, not a capability exposed externally. Strategy code accesses nodes through `ISndEntity` (composes `ISndNodeAccess`) without needing to know the node container's recovery/reclamation lifecycle. internal visibility prevents strategy code from bypassing the entity to directly manipulate the node pool.

### Why INodeHandle does not expose native node objects
Core operates on nodes through `INodeHandle` methods (`Free` / `SetVisible`), holding and exposing no engine-specific types. When a native node is needed, the adapter layer's `SndEntityNodeExtensions` (namespace `Origo.GodotAdapter.Snd`, file `Origo.GodotAdapter/SndEntityNodeExtensions.cs`) provides extension methods: `GetNativeNode()` extracts an `INodeHandle` to `Godot.Node?` (returns null when the handle is not a `GodotNodeHandle`), and `GetNodeFromSnd<T>()` resolves a node by logical name through the entity's SND node registry and casts it (an unregistered name throws `InvalidOperationException`; a type mismatch returns null). Engine node access is uniformly declared through these adapter-layer extensions; `INodeHandle` itself does not expose engine types via `object`, keeping Core isolated from engine types.

---
[↑ Back to Abstractions](../README.en.md)
