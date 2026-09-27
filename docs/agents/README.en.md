# Agent skills configuration

> [↑ Back to Origo Manual](../README.en.md)

This directory connects Matt Pocock's engineering skills to Origo. The skills read three files at fixed paths; those English files are tool-facing configuration for the English manual. Repository-root [AGENTS.md](../../AGENTS.md) and [META](../META.en.md) remain authoritative.

| Configuration | Responsibility |
|---------------|----------------|
| `issue-tracker.md` | GitHub Issues at `51193/origo` and one local counterpart per issue under `.scratch/issues/`, matched by number |
| `triage-labels.md` | Names of five triage states in GitHub labels and local `Status:` fields |
| `domain.md` | Single-context reading and recording paths using the English manual and `docs/architecture/` |

## Design decisions

- The GitHub issue number is the shared identity. Local counterparts support search, offline preparation, and skill workflows; published remote state and local content require explicit synchronization.
- Domain terms and architecture decisions stay in the existing manual, module docs, and [architecture docs](../architecture/README.en.md), avoiding a second `CONTEXT.md` or `docs/adr/` tree.
- The three fixed paths are skill configuration entry points; the English README is the manual entry point. Update it when configuration behavior changes.

---
[↑ Back to Origo Manual](../README.en.md)
