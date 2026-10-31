# Origo Manual

This is the entry point to the Origo documentation. Origo is a lightweight,
engine-agnostic C# game framework whose goal is an RPG Maker-style workflow:
define game content and scenes, and let the framework own orchestration and
persistence.

## Choose a path

| You are... | Start with |
|---|---|
| Evaluating Origo | [Capabilities](usage/capabilities.en.md) and [Architecture Overview](architecture/overview.en.md) |
| Building a game | [Usage Documentation](usage/README.en.md), then the [SND Entity Model](usage/snd-entity-model.en.md) |
| Using an AI agent | [Agent Workflow](../AGENTS.md), [Agent Reference](usage/agent-reference.en.md), and the [generated contract API](generated/contract-api.en.md) |
| Maintaining the framework | [Contributing](../CONTRIBUTING.md), [META](META.en.md), [Release Process](release-process.en.md), and [Review Process](review-process.en.md) |
| Looking for a specific module | [Project Module Index](#project-module-index) |

A runnable game demo and a getting-started walkthrough are intentionally not
published yet; the demo is still being built. Until then, use the capability
map, architecture documents, and the tested
[package-consumer fixture](usage/shell-package-consumer.en.md) rather than an
unverified walkthrough.

## What Origo owns

- **Runtime orchestration**: frame driving, deferred queues, startup, and
  shutdown.
- **Entity lifecycle**: spawn, load, add/remove, kill, save, quit, and dead
  transitions, with strategy hooks in one orchestrated path.
- **Sessions**: foreground and background sessions with one lifecycle model.
- **Persistence**: the `current/` workspace, snapshot slots, strict reads, and
  interruption recovery.
- **Engine boundary**: a contract layer plus the official Godot adapter; the
  Core layer does not depend on an engine.

Game code supplies strategies, scene/data content, and entry configuration.
The framework does not require game code to duplicate its runtime wiring.

## Documentation map

| Area | Entry |
|---|---|
| Usage and task guides | [usage/README](usage/README.en.md) |
| Complete capability map | [usage/capabilities](usage/capabilities.en.md) |
| Architecture and decisions | [architecture/README](architecture/README.en.md) |
| Runtime, SND, save, and console internals | [Project Module Index](#project-module-index) |
| Tests and their capabilities | [Test documentation](#test-documentation) |
| Repository rules | [AGENTS.md](../AGENTS.md) |
| Review and pull-request process | [review-process](review-process.en.md) |
| Repository agent configuration | [agents/README](agents/README.en.md) |
| Documentation maintenance | [META](META.en.md) |
| Release and Changelog | [release-process](release-process.en.md) |
| Performance baselines | [benchmarks/README](benchmarks/README.en.md) |
| Generated shell contract API | [generated/contract-api](generated/contract-api.en.md) |
| Generated documentation conventions | [generated/README](generated/README.en.md) |

## Project Module Index

| Module | Documentation |
|---|---|
| `Origo.Core.Contracts` | [Contracts](Origo.Core.Contracts/README.en.md) |
| `Origo.Core.Kernel` | [Kernel](Origo.Core.Kernel/README.en.md) |
| `Origo.Core` | [Consumer shell](Origo.Core/README.en.md) |
| `Origo.SourceGeneration` | [Source generator](Origo.SourceGeneration/README.en.md) |
| `Origo.GodotAdapter` | [Godot adapter](Origo.GodotAdapter/README.en.md) |
| `Origo.ConsoleBridge` | [Console bridge](Origo.ConsoleBridge/README.en.md) |
| `Origo.TestSupport` | [Test support](Origo.TestSupport/README.en.md) |

SND is split across layers rather than owned by one directory:

- [Contracts/Snd](Origo.Core.Contracts/Snd/README.en.md) — data, metadata,
  strategy bases, and role interfaces.
- [Kernel/Snd](Origo.Core.Kernel/Snd/README.en.md) — context, world, entity,
  scene host, strategy pool, and observer topology.
- [Core/Snd](Origo.Core/Snd/README.en.md) — consumer shell helpers.

## Test documentation

- [Core tests](Origo.Core.Tests/README.en.md)
- [Godot adapter tests](Origo.GodotAdapter.Tests/README.en.md)
- [Godot integration tests](Origo.GodotAdapter.Integration.Tests/README.en.md)
- [Console bridge tests](Origo.ConsoleBridge.Tests/README.en.md)
- [Source generator tests](Origo.SourceGeneration.Tests/README.en.md)

## Version and compatibility

The source version is defined by `<Version>` in
[Directory.Build.props](../Directory.Build.props). The release workflow stamps
the exact release version on the tag-only release commit. The bounded 0.1.x
shell compatibility promise is documented in
[shell-kernel-boundary](architecture/shell-kernel-boundary.en.md); the kernel
implementation package makes no consumer compatibility promise.

## Documentation tooling

Generated references are produced locally and committed, because the repository
does not rely on a remote documentation service:

```bash
bash scripts/bash/docs.sh generate
bash scripts/bash/docs.sh check
```

Windows uses `scripts/powershell/docs.ps1` with the same modes. Both local lint
gates run the check automatically.
