<!-- docsync-pair: architecture/extension-directions -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Extension Directions and Deferred Designs

> [↑ Back to Architecture](README.en.md)

> **Nature of this document**: This page is an index of deferred design directions. The complete context, design gate, re-evaluation signal, and acceptance criteria for each direction live in its GitHub issue. The page no longer carries the full ideas, so it cannot become a second source of backlog truth. Update it only when a direction is added, removed, or changes state.

Before reading this page, understand the current baseline: [Architecture Overview](overview.en.md), [SND Entity Model](../usage/snd-entity-model.en.md), [Strategy Lifecycle](../usage/strategy-lifecycle.en.md), and [Design Patterns](../usage/design-patterns.en.md).

## Direction Index

| Direction | State | Tracking issue | Notes |
|-----------|-------|----------------|-------|
| Unified tree namespace | blocked-design | [#44](https://github.com/51193/origo/issues/44) | Restricted tree root, content/metadata boundary, and remote async I/O are undecided |
| Entity-level concurrency | waiting-signal | [#45](https://github.com/51193/origo/issues/45) | Waiting for a profiling signal; in-entity strategy order stays serial |
| Multiple ActiveStrategy implementations per index | blocked-design | [#46](https://github.com/51193/origo/issues/46) | Contract identity, per-entity binding, and recovery semantics are undecided |

## Maintenance Rules

- A deferred direction is tracked only by its GitHub issue; this page keeps a one-line index and current state.
- When an issue is implemented, cancelled, or split, update this page in the same change and keep both languages aligned.
- The full trade-off lives in the issue body and the historical version at the frozen commit; do not copy long ideas back into module READMEs.

## Related Documents

- Current architecture: [Architecture Overview](overview.en.md)
- Strategy implementation: [Strategy Module](../Origo.Core/Snd/Strategy/README.en.md)
- DataSource implementation: [DataSource Module](../Origo.Core.Kernel/DataSource/README.en.md)
- Scheduling implementation: [Scheduling Module](../Origo.Core.Kernel/Scheduling/README.en.md)
- Entity implementation: [Entity Module](../Origo.Core.Kernel/Snd/Entity/README.en.md)
- Common patterns: [Design Patterns](../usage/design-patterns.en.md)

---
[↑ Back to Architecture](README.en.md)
