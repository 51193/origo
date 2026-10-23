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
| `SetEntityDataCommandHandler.cs` | `entity_set_data` | Sets entity data (preserves existing type; non-finite float literals are rejected) |
| `InvokeStrategyCommandHandler.cs` | `invoke_strategy` | Invokes active strategy with optional JSON input |
| `ListSavesCommandHandler.cs` | `list_saves` | Lists save slots and display metadata |
| `SaveGameCommandHandler.cs` | `save` | Queues a save request for the named slot |
| `LoadGameCommandHandler.cs` | `load` | Validates that the slot exists, then queues a load request |
| `DeleteSaveCommandHandler.cs` | `delete_save` | Deletes an inactive slot and its `.tmp`/`.bak` remnants |
| `SwitchLevelCommandHandler.cs` | `switch_level` | Queues a foreground-level switch request |

## Command Details

### bb_get

```
bb_get <layer> <key>
```
Reads the key value from the given blackboard layer. Currently only `layer=system` is supported.

### bb_set

Syntax and type-inference rules: [Usage: Console Commands — bb_set](../../../../usage/console-commands.en.md#bb_set).

### spawn

```
spawn <name> <template>
spawn name=<name> template=<template>
```
Spawns an entity from a template. Mixing positional and named arguments is not supported. Templates resolve via `SndWorld.ResolveTemplate`; an **unknown template alias** (or templates not yet loaded) and a **duplicate entity name** are treated as user input errors and return an error message (consistent with `bb_get`'s unknown-layer handling), never breaking out of the command loop. An unregistered strategy index or a throwing `AfterSpawn` hook still propagates as a real configuration/strategy failure.

### invoke_strategy

```
invoke_strategy <entity> <strategy_index> [input]
```
Finds the entity by name, invokes the active strategy via `ISndActiveStrategyAccess.InvokeStrategy`, and prints the return value. `input` is optional, supports a JSON string, and is parsed by the strategy itself.

### list_saves

Syntax and output format: [Usage: Console Commands — list_saves](../../../../usage/console-commands.en.md#list_saves--save--load--delete_save--switch_level).

### save / load / switch_level

Deferred-queue semantics and syntax: [Usage: Console Commands — save / load / switch_level](../../../../usage/console-commands.en.md#list_saves--save--load--delete_save--switch_level).

### delete_save

```
delete_save <saveId>
```
Synchronously deletes an inactive save slot and its `.tmp`/`.bak` remnants. The active save, the continue target, and running or pending persistence requests are rejected with an error.

## Design Decisions

### Why only bb_get/bb_set support system layer
Early runtime phase; progress/session blackboards are null before flow/session starts.

### Why bb_set auto-infers types
Manual type specification would reduce debug efficiency. Auto-inference covers 95% of needs.

### Why spawn supports named args
Named args are more readable with long template names and reserve space for future extension.

---
[↑ Back to Console](../README.en.md)
