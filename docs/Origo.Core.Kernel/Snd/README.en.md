# Snd (Kernel)

> [↑ Back to Origo.Core.Kernel](../README.en.md)

Kernel implementation of the SND context/world, entity aggregate, scene host,
strategy/observer machinery, and their internal file companions.

## Sub-modules

| Sub-module | Capability | Details |
|------------|------------|---------|
| [Companions](Companions/README.en.md) | Internal `SndContext` role companions | Blackboard / deferred / template / console / state-machine / save / lifecycle / state-machine-context views |
| [Entity](Entity/README.en.md) | SND entity aggregate implementation | Entity data, node, passive/active strategy managers, and raw-subscription plumbing |
| [Scene](Scene/README.en.md) | Scene host and entity factory | Full-memory scene host, entity naming policy, spawn rollback, and node factory |
| [Strategy](Strategy/README.en.md) | Strategy pool, managers, and observer topology | Lifecycle ordering, active/passive managers, pool, and observer binding topology |

## Included Files

| File | Responsibility |
|------|----------------|
| `SndContext.cs` | See source documentation and API comments. |
| `SndContextArchiveFileAccess.cs` | See source documentation and API comments. |
| `SndContextFileAccess.cs` | See source documentation and API comments. |
| `SndContextParameters.cs` | See source documentation and API comments. |
| `SndDefaults.cs` | See source documentation and API comments. |
| `SndMappings.cs` | See source documentation and API comments. |
| `SndTemplateResolver.cs` | See source documentation and API comments. |
| `SndWorld.cs` | See source documentation and API comments. |

---
[↑ Back to Origo.Core.Kernel](../README.en.md)
