<!-- docsync-pair: Origo.Core/Runtime/Console/CommandHandlers/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# CommandHandlers

> [↑ Back to Console](../README.en.md) · [↔ Usage: console-commands](../../../../usage/console-commands.en.md)

## Overview
Concrete built-in console command handlers. All are `internal`, registered via `OrigoConsole`.

## Included Files

| File | Command | Function |
|------|------|------|
| `HelpCommandHandler.cs` | `help` | Lists all registered commands |
| `BlackboardGetCommandHandler.cs` | `bb_get` | Reads key value from blackboard layer |
| `BlackboardSetCommandHandler.cs` | `bb_set` | Writes value (auto-infer type) |
| `BlackboardKeysCommandHandler.cs` | `bb_keys` | Lists all key names in a layer |
| `SpawnTemplateCommandHandler.cs` | `spawn` | Spawns entity from template |
| `FindEntityCommandHandler.cs` | `find_entity` | Finds entity by name, shows node info |
| `KillAllCommandHandler.cs` | `kill_all` | Marks all entities for destruction (end of frame) |
| `SndCountCommandHandler.cs` | `snd_count` | Displays entity count |
| `GetEntityDataCommandHandler.cs` | `entity_get_data` | Reads entity data (value + type) |
| `SetEntityDataCommandHandler.cs` | `entity_set_data` | Sets entity data (preserves existing type) |
| `InvokeStrategyCommandHandler.cs` | `invoke_strategy` | Invokes active strategy with optional JSON input |

## Command Details

### bb_get

```
bb_get <layer> <key>
```
Reads the key value from the given blackboard layer. Currently only `layer=system` is supported.

### bb_set

```
bb_set <layer> <key> <value>
```
Writes a value to the blackboard. Value type is auto-inferred: integer → Int32, float → Single, "true"/"false" → Boolean, otherwise → String.

### spawn

```
spawn <name> <template>
spawn name=<name> template=<template>
```
Spawns an entity from a template. Mixing positional and named arguments is not supported. Templates resolve via `SndWorld.ResolveTemplate`; an **unknown template alias** (or templates not yet loaded) is treated as a user input error and returns an error message (consistent with `bb_get`'s unknown-layer handling), never breaking out of the command loop.

### invoke_strategy

```
invoke_strategy <entity> <strategy_index> [input]
```
Finds the entity by name, invokes the active strategy via `ISndActiveStrategyAccess.InvokeStrategy`, and prints the return value. `input` is optional, supports a JSON string, and is parsed by the strategy itself.

## Design Decisions

### Why only bb_get/bb_set support system layer
Early runtime phase; progress/session blackboards are null before flow/session starts.

### Why bb_set auto-infers types
Manual type specification would reduce debug efficiency. Auto-inference covers 95% of needs.

### Why spawn supports named args
Named args are more readable with long template names and reserve space for future extension.

---
[↑ Back to Console](../README.en.md)
