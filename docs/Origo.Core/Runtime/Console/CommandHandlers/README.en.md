<!-- docsync-pair: Origo.Core/Runtime/Console/CommandHandlers/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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

## Design Decisions

### Why only bb_get/bb_set support system layer
Early runtime phase; progress/session blackboards are null before flow/session starts.

### Why bb_set auto-infers types
Manual type specification would reduce debug efficiency. Auto-inference covers 95% of needs.

### Why spawn supports named args
Named args are more readable with long template names and reserve space for future extension.

---
[↑ Back to Console](../README.en.md)
