# Usage Documentation

> [↑ Back to Origo Manual](../README.en.md)

Task-oriented documentation for developers and agents building games with
Origo. The framework goal is to let game code describe content, scenes, and
strategies while Origo owns runtime orchestration and persistence.

A runnable game demo and a getting-started walkthrough are not published yet.
The demo is still being built; do not treat an incomplete sample as a supported
entry point. Use the capability map, task documents, and the
[package-consumer fixture](shell-package-consumer.en.md) instead.

## Start by task

| I need to... | Read |
|---|---|
| Understand what the framework provides | [Capabilities](capabilities.en.md) |
| Learn the entity/strategy/data model | [SND Entity Model](snd-entity-model.en.md) |
| Understand strategy hook order and teardown | [Strategy Lifecycle](strategy-lifecycle.en.md) |
| Write and test strategies | [Strategy Testing](strategy-testing.en.md) |
| Work with foreground/background sessions | [Session Model](session-model.en.md) |
| Use the save system and recovery rules | [Persistence Flow](persistence-flow.en.md) |
| Use the string-stack state machine | [State Machine](state-machine.en.md) |
| Inspect or drive a running game | [Console Commands](console-commands.en.md) |
| Write reusable game architecture | [Design Patterns](design-patterns.en.md) |
| Have an agent work on runtime behavior | [Agent Reference](agent-reference.en.md) and [generated contract API](../generated/contract-api.en.md) |
| Inspect the shell package consumer path | [Shell Package Consumer](shell-package-consumer.en.md) |
| Develop on Windows | [Windows Development](windows-development.en.md) |

## Recommended paths

```text
New to Origo:
  Capabilities -> Architecture Overview -> SND Entity Model -> Strategy Lifecycle

Writing gameplay:
  SND Entity Model -> Strategy Lifecycle -> Design Patterns -> Strategy Testing

Sessions and saving:
  Session Model -> Persistence Flow -> Capabilities (security and limits)

Agent work:
  Agent Reference -> generated contract API -> the task-specific usage page
```

## Documentation boundaries

The usage pages describe **how to build with the framework**. Module pages
describe **how the framework is implemented**. Generated API pages are the
source of truth for the public shell signatures; hand-written task pages link
to them rather than copying full interface declarations.

| System | Usage entry | Implementation entry |
|---|---|---|
| SND entity model | [SND Entity Model](snd-entity-model.en.md) | [Contracts/Snd](../Origo.Core.Contracts/Snd/README.en.md), [Kernel/Snd](../Origo.Core.Kernel/Snd/README.en.md) |
| State machine | [State Machine](state-machine.en.md) | [Kernel/StateMachine](../Origo.Core.Kernel/StateMachine/README.en.md) |
| Persistence | [Persistence Flow](persistence-flow.en.md) | [Kernel/Save](../Origo.Core.Kernel/Save/README.en.md) |
| Console | [Console Commands](console-commands.en.md) | [Kernel/Console](../Origo.Core.Kernel/Runtime/Console/README.en.md) |
| Godot adapter | [Capabilities](capabilities.en.md) | [Origo.GodotAdapter](../Origo.GodotAdapter/README.en.md) |

---
[↑ Back to Origo Manual](../README.en.md)
