<!-- docsync-pair: Origo.Core.Contracts/Abstractions/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Abstractions

> [↑ Back to Origo.Core.Contracts](../README.en.md)

## Module Capability

Base abstraction layer of Origo.Core.Contracts: leaf interfaces implemented by
platform hosts. Every interface uses only `System.*` types and references no Core
runtime implementation or concrete adapter.

## Sub-modules

| Sub-module | Capability | Details |
|------------|------------|---------|
| [Blackboard](Blackboard/README.en.md) | Key-value blackboard contract | `IBlackboard` |
| [Console](Console/README.en.md) | Console input/output abstraction | `IConsoleInputSource` + `IConsoleOutputChannel` |
| [Entity](Entity/README.en.md) | SND entity role contracts | `ISndEntity` and narrow interfaces |
| [FileSystem](FileSystem/README.en.md) | Platform file-system and path abstraction | `IFileSystem` + `IPathResolver` |
| [Lifecycle](Lifecycle/README.en.md) | Session lifecycle contracts | `ISessionManager` + `ISessionRun` |
| [Logging](Logging/README.en.md) | Engine-agnostic logging interfaces | `ILogger` + `ILogger<TCategory>` + `LogLevel` |
| [Node](Node/README.en.md) | Engine node abstraction | `INodeFactory` + `INodeHandle` |
| [Runtime](Runtime/README.en.md) | Frame-driver contract | `IOrigoFrameDriver` |
| [Scene](Scene/README.en.md) | Read-only scene-access contract | `ISndSceneReadAccess` |
| [Snd](Snd/README.en.md) | SND companion contracts | Nine `ISnd*` role interfaces |
| [StateMachine](StateMachine/README.en.md) | State-machine contracts | `IStateMachine` + `IStateMachineContainer` + `IStateMachineContext` |

## Files at This Level

This directory contains only sub-directories and no direct `.cs` files.

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
