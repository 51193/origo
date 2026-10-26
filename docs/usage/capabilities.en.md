# Capabilities

> [↑ Back to Usage Documentation](README.en.md)

What Origo provides, by capability area. The framework goal is an RPG
Maker-style division of labour: describe game content and scenes, and let the
framework own orchestration and persistence.

## Host and runtime

| Capability | Description | Documentation |
|---|---|---|
| Host facade | `OrigoHost` creates a platform-independent host and exposes the stable runtime/context surfaces | [Origo.Core](../Origo.Core/README.en.md) |
| Bootstrap | One Core-owned startup sequence: strategy discovery/validation, alias/template loading, then entry load | [Architecture](../architecture/overview.en.md) |
| Shutdown | Idempotent teardown that runs quit hooks, releases the progress run, and removes the temporary working area | [Generated API](../generated/contract-api.en.md) |
| Frame ownership | Core owns frame order: entity processing, deferred queues, pending kills, then console commands | [Architecture](../architecture/overview.en.md) |
| Deferred scheduling | Thread-safe business/system queues with snapshot-and-drain execution | [Scheduling](../Origo.Core.Kernel/Scheduling/README.en.md) |

## Entities and strategies

| Capability | Description | Documentation |
|---|---|---|
| SND model | Strategy (behavior), Node (presentation), and Data (state) are separate concerns | [SND Entity Model](snd-entity-model.en.md) |
| Strategy hooks | `AfterSpawn`, `AfterLoad`, `AfterAdd`, `Process`, `BeforeRemove`, `BeforeSave`, `BeforeQuit`, `BeforeDead` | [Strategy Lifecycle](strategy-lifecycle.en.md) |
| Strategy pool | Stateless shared strategies, validated at registration and released by reference counting | [Kernel/Snd/Strategy](../Origo.Core.Kernel/Snd/Strategy/README.en.md) |
| Strategy ordering | Relative `Before`/`After` ordering projected over the complete registry | [Architecture](../architecture/overview.en.md) |
| TypedData | Generated typed accessors and layered registrations; deserialization preserves the registered type | [SND Entity Model](snd-entity-model.en.md) |
| Data observation | Observer strategies react to entity data changes; bindings persist across saves and are restored on load | [SND Entity Model](snd-entity-model.en.md) |
| Cross-entity observation | Mount an observer against a target entity with lifecycle-aware mount/unmount | [SND Entity Model](snd-entity-model.en.md) |
| Active strategies | Indexed, externally invoked services with typed generic helpers | [Strategy Testing](strategy-testing.en.md) |
| Runtime strategy changes | Add/remove passive, active, and observer strategies with orchestrated lifecycle side effects | [Strategy Lifecycle](strategy-lifecycle.en.md) |
| Metadata builder | `SndMetaFluentBuilder` for entity metadata, lifecycle/active indices, and observer bindings | [Metadata](../Origo.Core.Contracts/Snd/Metadata/README.en.md) |
| Numeric reads | `TryGetNumeric`/`GetNumeric` bridge compatible numeric types without string serialization | [Core/Snd](../Origo.Core/Snd/README.en.md) |
| Archetypes and templates | Load key-value entity recipes and clone scene templates through the data-source boundary | [Core/Snd](../Origo.Core/Snd/README.en.md) |

## Sessions

| Capability | Description | Documentation |
|---|---|---|
| Foreground/background sessions | Both use `ISessionRun` and the same strategy pipeline; no parallel entity model | [Session Model](session-model.en.md) |
| Session topology | The active session set, keys, levels, and processing flags are encoded in the progress data | [Session Model](session-model.en.md) |
| Level identity | A level is held by at most one session; conflicts fail before mounting | [Session Model](session-model.en.md) |
| Level switching | Foreground switches persist the old level, resolve the target, mount it, and persist the new topology as one ordered operation | [Persistence Flow](persistence-flow.en.md) |

## Persistence

| Capability | Description | Documentation |
|---|---|---|
| Two-phase write | Write the active `current/` workspace under a marker, then install a snapshot after validation | [Persistence Flow](persistence-flow.en.md) |
| Strict reads | Marker/three-file/format checks fail explicitly instead of accepting partial state | [Persistence Flow](persistence-flow.en.md) |
| Snapshot operations | Enumerate, inspect metadata, delete inactive slots, and choose a continue target | [Persistence Flow](persistence-flow.en.md) |
| Idempotent writes | Payload hashing avoids rewriting an unchanged snapshot | [Storage](../Origo.Core.Kernel/Save/Storage/README.en.md) |
| Display metadata | `ISaveMetaContributor` adds display-only metadata without mixing it into gameplay state | [Persistence Flow](persistence-flow.en.md) |
| Completion observation | `IsPersistenceIdle` and `IsBootstrapCompleted` expose frame-driven completion | [Persistence Flow](persistence-flow.en.md) |

## State machines and planning

| Capability | Description | Documentation |
|---|---|---|
| String-stack state machine | A stack of identifiers whose behavior is supplied by push/pop strategies | [State Machine](state-machine.en.md) |
| Lifecycle-aware stack hooks | Runtime, after-load, and before-quit hooks with explicit stack-top context | [State Machine](state-machine.en.md) |
| Plan execution | `PlanExecutionStrategyBase` runs action sequences with explicit completion/failure and hook teardown | [Planning](../Origo.Core.Contracts/Planning/README.en.md) |

## Console

| Capability | Description | Documentation |
|---|---|---|
| Built-in commands | Entity inspection, data access, strategy invocation, save-slot operations, and help (the command reference is authoritative) | [Console Commands](console-commands.en.md) |
| Custom commands | Register command handlers through the public runtime surface | [Console Commands](console-commands.en.md) |
| TCP bridge | Loopback-only, single-connection console bridge; no authentication or encryption, intended for local debugging and automation | [ConsoleBridge](../Origo.ConsoleBridge/README.en.md) |
| Bounded input | The bridge bounds line length and the Core queue bounds pending commands and per-frame work | [ConsoleBridge](../Origo.ConsoleBridge/README.en.md) |

## Data and serialization

| Capability | Description | Documentation |
|---|---|---|
| DataSource tree | A single tree model over map/array/text/number/bool/null values with lazy expansion | [DataSource](../Origo.Core.Kernel/DataSource/README.en.md) |
| Codecs | JSON and `.map` codecs behind the gateway; no direct file-content bypass | [Codec](../Origo.Core.Kernel/DataSource/Codec/README.en.md) |
| Type-string mapping | Stable CLR type names for persistence and adapter registration | [Serialization](../Origo.Core.Contracts/Serialization/README.en.md) |
| Godot types | Godot engine values round-trip through registered converters and TypedData layers | [GodotAdapter/Serialization](../Origo.GodotAdapter/Serialization/README.en.md) |
| File access | Strategies use `ISndFileAccess` for resources and `ISndArchiveFileAccess` for save-scoped files | [Generated API](../generated/contract-api.en.md) |

## Godot adapter

| Capability | Description | Documentation |
|---|---|---|
| File system | `res://` and `user://` access with path traversal protection | [GodotAdapter/FileSystem](../Origo.GodotAdapter/FileSystem/README.en.md) |
| Logging bridge | Core logging forwarded to Godot output facilities | [GodotAdapter/Logging](../Origo.GodotAdapter/Logging/README.en.md) |
| Packed scenes | Logical scene aliases resolve to instantiated engine nodes | [GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.en.md) |
| Entity bridge | `GodotSndEntity` connects the Core entity lifecycle to Godot nodes without owning lifecycle decisions | [GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.en.md) |
| Adapter console | `tree_debug`, `camera_view`, and `press_button` expose engine-side diagnostics | [GodotAdapter/Console](../Origo.GodotAdapter/Console/README.en.md) |

## Random, grid, and utilities

| Capability | Description | Documentation |
|---|---|---|
| Deterministic PRNG | `XorShift128+` and save-safe `PersistentRandom` | [Random](../Origo.Core/Random/README.en.md) |
| Noise | OpenSimplex2 and Worley-based noise maps for procedural content | [Random](../Origo.Core/Random/README.en.md) |
| Grid and pathfinding | Grid coordinates, coordinate parsing, and A* pathfinding | [Grid](../Origo.Core/Grid/README.en.md) |
| Blackboard | Case-sensitive in-memory key-value state with serialization | [Blackboard](../Origo.Core.Contracts/Blackboard/README.en.md) |

## Design properties

| Property | Description |
|---|---|
| Engine isolation | Core has no engine dependency; the adapter implements Core abstractions and never owns lifecycle decisions |
| Fail-fast validation | Invalid IDs, partial saves, unknown strategies, and contract violations fail explicitly |
| Single access path | Capabilities are reached through their public interfaces and framework orchestration, not hand-stitched low-level calls |
| Public boundary | Stable consumer contracts are separated from kernel implementation details and checked by the shell API baseline |

---
[↑ Back to Usage Documentation](README.en.md)
