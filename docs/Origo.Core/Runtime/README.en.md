<!-- docsync-pair: Origo.Core/Runtime/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Runtime

> [↑ Back to Origo.Core](../README.en.md)

## Module Capability

Origo's runtime core. Manages the four-layer lifecycle from system-level to session-level, provides the console command system, session manager, and state machine container.

## Sub-Modules

| Sub-Module | Capability | Details |
|-----------|-----------|---------|
| [Console](Console/README.en.md) | Console command system | Command parsing/routing + input queue + output channel |
| [Console/CommandHandlers](Console/CommandHandlers/README.en.md) | Built-in commands | help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy (11 total) |
| [Lifecycle](Lifecycle/README.en.md) | Four-layer runtime lifecycle | SystemRun → ProgressRun → SessionManager → SessionRun |
| [StateMachine](StateMachine/README.en.md) | State machine container | `StateMachineContainer`: CreateOrGet / serialization / batch operations |

## This Layer's Core Files

| File | Responsibility |
|------|---------------|
| `OrigoRuntime.cs` | Runtime aggregation container: holds SystemBlackboard, SndWorld, Console, Logger; SndContext constructs SystemRun and ProgressRun on top of it |
| `OrigoAutoInitializer.cs` | `internal` — automatic strategy discovery and registration (reflection-based assembly scanning); invoked only by `SndContext.Bootstrap` orchestration |

### OrigoRuntime

Runtime entry point; centrally holds references to all runtime subsystems:

```
OrigoRuntime
├── OrigoMeta (name/version/banner)
├── ILogger
├── IBlackboard (SystemBlackboard + PersistentBlackboard)
├── SndWorld (strategy pool + type mapping + converters)
├── OrigoConsole (console command routing)
├── IOrigoFrameDriver (frame loop driver)
└── (held by SndContext) SystemRun (system-level lifecycle)
    └── ProgressRuntime → ProgressRun
        └── SessionManagerRuntime → SessionManager
            └── SessionRun (foreground + background)
```

## Runtime Four Layers

```
SystemRuntime → SystemRun
    ├── SndWorld (globally shared)
    ├── SystemBlackboard
    └── ProgressRuntime → ProgressRun
        ├── ProgressBlackboard
        ├── SaveContext (save orchestration)
        └── SessionManagerRuntime → SessionManager
            ├── SessionRun (foreground: "__foreground__")
            │   ├── SessionBlackboard
            │   ├── ISndSceneHost (Godot or in-memory)
            │   └── StateMachineContainer
            └── SessionRun (background: user-defined keys)
                ├── SessionBlackboard
                ├── FullMemorySndSceneHost
                └── StateMachineContainer
```

Capabilities flow one-way downward; lower layers must not depend on upper layers in reverse.

---
[↑ Back to Origo.Core](../README.en.md)
