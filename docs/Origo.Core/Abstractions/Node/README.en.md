<!-- docsync-pair: Origo.Core/Abstractions/Node/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Node (Core Internal Host)

> [↑ Back to Abstractions](../README.en.md) · [↔ Consumer contracts: Origo.Core.Contracts/Node](../../../Origo.Core.Contracts/Abstractions/Node/README.en.md)

## Overview

Core-internal node container contract `INodeHost`: manages node recovery,
reclamation, and metadata export. Consumer/adapter-facing `INodeFactory` and
`INodeHandle` live in
[Origo.Core.Contracts/Abstractions/Node](../../../Origo.Core.Contracts/Abstractions/Node/README.en.md).

## Included Files

| File | Responsibility |
|------|----------------|
| `INodeHost.cs` | internal: node container behavior — recovery, reclamation, metadata export |

## Interface Details

### INodeHost (internal)

| Member | Description |
|------|-------------|
| `GetNode(name)` | Get node handle by name |
| `GetNodeNames()` | Enumerate mounted node names |
| `Recover(NodeMetaData)` | Recover node from metadata |
| `Release()` | Reclaim all nodes |
| `SerializeMetaData()` | Export node metadata |

## Design Decisions

### Why INodeHost is internal

`INodeHost` is the contract for SND entities' internal node management, not an
externally exposed capability. Strategy code accesses nodes through
`ISndEntity` (which composes `ISndNodeAccess`) without needing to know the node
container's recovery/reclamation lifecycle. internal visibility prevents
strategy code from bypassing the entity to manipulate the node pool directly.

---
[↑ Back to Abstractions](../README.en.md)
