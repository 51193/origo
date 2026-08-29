<!-- docsync-pair: usage/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Usage Documentation

> [↑ Back to Origo Manual](../README.en.md)

## Document Index

Documentation for Origo framework users (game developers, AI agents). Organized by usage scenario, from beginner onboarding to deep reference.

| Document | Target Audience | Description |
|----------|----------------|-------------|
| [quick-start](quick-start.en.md) | New users | Integrate Origo into a Godot 4 project in 5 minutes |
| [architecture-overview](architecture-overview.en.md) | Developers | Four-layer runtime architecture, SND model, persistence, concurrency model |
| [snd-entity-model](snd-entity-model.en.md) | Strategy developers | Strategy + Node + Data model in detail, strategy writing guide |
| [session-model](session-model.en.md) | Advanced developers | Foreground/background sessions, session lifecycle, topology codec |
| [persistence-flow](persistence-flow.en.md) | Advanced developers | Two-phase write, strict read, file layout, save recovery |
| [state-machine](state-machine.en.md) | Strategy developers | String-stack state machine, Push/Pop hooks, persistence |
| [console-commands](console-commands.en.md) | Debuggers | Complete reference of built-in console commands |
| [strategy-lifecycle](strategy-lifecycle.en.md) | Strategy developers | Lifecycle hook closed-loop pairing, RAII resource management, BeforeSave deferred sync |
| [design-patterns](design-patterns.en.md) | Strategy developers | Naming conventions, Manager service pattern, replaceable implementations, template best practices |
| [extension-directions](extension-directions.en.md) | Developers / AI agents | Brainstormed extension directions and deferral reasons: unified tree namespace, entity-level concurrency, relative ordering constraints, multiple ActiveStrategy implementations per index |
| [strategy-testing](strategy-testing.en.md) | Test authors | StrategyTestScenario usage guide |
| [capabilities](capabilities.en.md) | All users | Complete framework capability checklist, indexed by functional domain — quickly understand what Origo can do |
| [agent-reference](agent-reference.en.md) | AI agents | Complete runtime reference: interface signatures, lifecycle timeline, strategy writing templates |

## Recommended Reading Paths

```
New users:
  quick-start → capabilities (browse all capabilities) → architecture-overview → snd-entity-model

Strategy developers:
  snd-entity-model → strategy-lifecycle → design-patterns → state-machine → strategy-testing

Facing architecture problems / need design inspiration:
  architecture-overview → design-patterns → extension-directions

Save system users:
  architecture-overview → persistence-flow → session-model

Console debuggers:
  quick-start → console-commands

AI agents:
  agent-reference (complete reference)
```

## Related Module Documentation

The usage docs describe "how to use Origo"; the module docs describe "Origo's internal implementation". When you need to deeply understand a subsystem's internal code structure, consult the corresponding module docs:

| System in Usage Docs | Corresponding Module Docs |
|---------------------|---------------------------|
| SND entity model | [Origo.Core/Snd/](../Origo.Core/Snd/README.en.md) |
| State machine system | [Origo.Core/StateMachine/](../Origo.Core/StateMachine/README.en.md) |
| Persistence system | [Origo.Core/Save/](../Origo.Core/Save/README.en.md) |
| Console commands | [Origo.Core/Runtime/Console/](../Origo.Core/Runtime/Console/README.en.md) |
| Godot adapter | [Origo.GodotAdapter/](../Origo.GodotAdapter/README.en.md) |

---
[↑ Back to Origo Manual](../README.en.md)
