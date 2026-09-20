<!-- docsync-pair: Origo.Core.Kernel/Abstractions/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Abstractions

> [↑ Back to Origo.Core](../README.en.md)

## Module Capability

The stable public abstraction layer of Origo.Core. All interfaces are defined in this layer as platform-agnostic contracts, implemented concretely by downstream modules (Core implementation layer, Godot adapter layer, test layer). Follows the Interface Segregation Principle (ISP); each sub-module provides a cohesive set of interfaces.

## Sub-Modules

| Sub-Module | Capability | Details |
|-----------|-----------|---------|
| [Entity](Entity/README.en.md) | Internal SND entity lifecycle contract | `IEntityLifecycle` (internal); public entity role interfaces live in Contracts |
| [Node](Node/README.en.md) | Internal node container contract | `INodeHost` (internal); `INodeFactory` / `INodeHandle` live in Contracts |
| [Scene](Scene/README.en.md) | Internal SND scene orchestration contracts | `ISndSceneAccess` / `ISndSceneHost` / `IOwningSessionBindable` (internal); the read-only contract lives in Contracts |

> Public entity, session, scene, state-machine, SND companion, and leaf contracts
> live in the stable contract package
> [Origo.Core.Contracts](../../Origo.Core.Contracts/Abstractions/README.en.md).

## Interface Hierarchy

```
IBlackboard  IConsole*  IFileSystem  ILogger  INode*  (Contracts)
IOrigoFrameDriver (Contracts)  IScheduler (Kernel)

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
