<!-- docsync-pair: Origo.GodotAdapter.Tests/Architecture -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Architecture Guardrail Tests (Adapter Layer)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Bootstrap](../Origo.GodotAdapter/Bootstrap/README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Console](../Origo.GodotAdapter/Console/README.en.md)
> [↔ Behavior under test: usage/session-model](../usage/session-model.en.md)

## Behavior Under Test Overview

Verifies that the SndContext created by the Godot adapter layer correctly exposes full role capabilities
(blackboard access, deferred queue, session management, Save/Load, lifecycle, console, file access)
through public interfaces, with session lifecycle managed via `ISessionManager`;
and guards the public visibility of the adapter-layer `CommandHandlerBase`, allowing external projects
(e.g., origo.demo) to derive custom console command handlers.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `AdapterArchitectureGuardrailTests.cs` | SndContext public interface completeness (including ISndFileAccess / ISndArchiveFileAccess), background session creation/destruction/data read-write; `CommandHandlerBase` public visibility guardrail |

## AdapterArchitectureGuardrailTests Test Details

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `SndContext_AllRoleInterfaces_AreAccessibleThroughISndContext` | ISndContext can be cast to each role interface (Blackboard / Deferred / Save / Lifecycle / Console / FileAccess / ArchiveFileAccess) and used | Abstractions: ISndContext |
| `SndContext_ViaSessionManager_CanCreateAndDestroyBackgroundSessions` | Create background session via ISessionManager, read/write session blackboard, Contains check, DestroySession | session-model |
| `CommandHandlerBase_ShouldBePublic_SoExternalProjectsCanExtendIt` | `Origo.GodotAdapter.Console.CommandHandlerBase` is public (or nested public), external projects can derive from it | Origo.GodotAdapter/Console |
| `GodotSndEntity_LifecycleMethods_ShouldBeInternal` | `GodotSndEntity` implements `IEntityLifecycle` via explicit interface implementation, invisible to reflection; lifecycle is driven only by Core orchestration | Origo.GodotAdapter/Snd |
| `GodotSndEntity_GetNodeFromSnd_ShouldRemainPublic` | `SndEntityNodeExtensions.GetNodeFromSnd<T>()` stays public so external projects can reach Godot nodes | Origo.GodotAdapter/Snd |

## Test Support Strategy

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `InMemoryLogger` | `AdapterArchitectureGuardrailTests.cs` | Minimal `ILogger` stand-in, swallows all logs, used only to construct `OrigoRuntime` |
| `InMemorySndSceneHost` | `AdapterArchitectureGuardrailTests.cs` | In-memory `ISndSceneHost` stand-in, maintains entity list, supports create/remove/restore metadata list |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Real Godot engine runtime paths for role interfaces when adapter creates SndContext are not covered (tests use in-memory stand-in host) | Godot engine-level behavior not directly verified in tests | Origo.GodotAdapter/Bootstrap |
| Architecture guardrails for foreground + background session coexistence and cross-session switching not covered | Interface contracts under multi-session topology not verified | session-model |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
