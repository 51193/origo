<!-- docsync-pair: Origo.Core/Abstractions/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Abstractions

> [↑ Back to Origo.Core](../README.en.md)

## Module Capability

The stable public abstraction layer of Origo.Core. All interfaces are defined in this layer as platform-agnostic contracts, implemented concretely by downstream modules (Core implementation layer, Godot adapter layer, test layer). Follows the Interface Segregation Principle (ISP); each sub-module provides a cohesive set of interfaces.

## Sub-Modules

| Sub-Module | Capability | Details |
|-----------|-----------|---------|
| [Blackboard](Blackboard/README.en.md) | General key-value blackboard interface, preserves type info | `IBlackboard`: SetValue/Get + serialization |
| [Console](Console/README.en.md) | Console input/output abstraction | `IConsoleInputSource` (polling) + `IConsoleOutputChannel` (publish-subscribe) |
| [Entity](Entity/README.en.md) | SND entity's five capability interfaces + standalone lifecycle interface | `ISndEntity` = `ISndDataAccess` + `ISndNodeAccess` + `ISndStrategyAccess` + `ISndActiveStrategyAccess` + `ISndObserverStrategyAccess`; `IEntityLifecycle` is a standalone `internal` interface (for internal framework use) |
| [FileSystem](FileSystem/README.en.md) | Platform-agnostic file system abstraction | `IFileSystem`: 13 file/directory operations, including path combination and parent directory. Strategies do not use this interface directly; they go through `ISndFileAccess` (via the `IDataSourceIoGateway` boundary) |
| [Lifecycle](Lifecycle/README.en.md) | Session management abstraction interfaces | `ISessionManager` (session lifecycle) + `ISessionRun` (session runtime facade) |
| [Logging](Logging/README.en.md) | Engine-agnostic logging interface | `ILogger` + `LogLevel` enum (Debug/Info/Warning/Error) |
| [Node](Node/README.en.md) | Abstract engine node operations | `INodeFactory` + `INodeHandle` + `INodeHost` (internal) |
| [Runtime](Runtime/README.en.md) | Abstract frame driver interface | `IOrigoFrameDriver`: DriveFrame (the public unified frame entry; OrigoRuntime Enqueue/Flush/Reset pipeline methods are internal) |
| [Scene](Scene/README.en.md) | SND scene access and host | public `ISndSceneReadAccess` (GetEntities/FindByName) + internal `ISndSceneAccess` / `ISndSceneHost` (orchestration) |
| [Snd](Snd/README.en.md) | ISndContext 10 companion properties | IStateMachineContext also inherits some of them |
| [StateMachine](StateMachine/README.en.md) | String-stack state machine system | `IStateMachine` + `IStateMachineContext` + `IStateMachineContainer` |

## Interface Hierarchy

```
IBlackboard  IConsole*  IFileSystem  ILogger  IOrigoFrameDriver  INode*

ISessionManager  ISessionRun → IStateMachineContainer

ISndEntity ─── ISndDataAccess + ISndNodeAccess + ISndStrategyAccess
                + ISndActiveStrategyAccess + ISndObserverStrategyAccess

IEntityLifecycle                (Standalone internal interface, framework-internal, not a sub-interface of ISndEntity)

ISndContext ··· companion properties › ISndBlackboardAccess + ISndDeferredActions
                + ISndTemplateAccess + ISndConsoleAccess + ISndStateMachineAccess
                + ISndSaveOperations + ISndLifecycleOperations
                + ISndFileAccess + ISndArchiveFileAccess
                + IStateMachineContext

ISndSceneHost (internal) ─── ISndSceneAccess (internal)

IStateMachine ⟷ IStateMachineContext ⟷ IStateMachineContainer
                    │ (inherits ISndBlackboardAccess + ISndDeferredActions)
```

## Design Principles

- **Interface Segregation**: Large interfaces are split into small interfaces; consumers depend only on what they need (e.g., a strategy depends only on `ISndDataAccess`, not `ISndNodeAccess`)
- **Platform-agnostic**: All interfaces use only `System.*` types (`object` replaces `Godot.Node`)
- **public whitelist**: Do not expose interfaces preemptively for "maybe useful in the future"; every public interface must have a clear cross-assembly consumer
- **internal implementation interfaces**: e.g., `INodeHost` is internal, used only within Core

## This Layer's Files

This directory contains only sub-directories; there are no direct `.cs` files.

---
[↑ Back to Origo.Core](../README.en.md)
