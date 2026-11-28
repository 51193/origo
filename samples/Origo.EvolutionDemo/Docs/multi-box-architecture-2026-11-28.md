# Multi-Box Architecture — 2026-11-28

> [↑ Back to demo Docs](README.md)

## Problem

The current demo is one pond, one global unlock profile, and one simulation
controller. That makes “all unlocks” correct inside a run, but it gives the
player no way to build different gene pools and compare them. Build choice must
move from point scarcity to box-scoped content profiles, migration, and arenas.

## Decisions

### 1. A box owns its unlock profile

- `BoxUnlockProfile` is the box's fixed unlock set.
- A box may enable every unlock; no points or milestones gate the profile.
- `GameplayMode.Profile` gives a runtime that profile without purchase flow.
- Gene founders and mutations use the owning box's `GenomeContentGate`.
- No process-global genome filter is allowed.

### 2. A box is a session

- The foreground box is the visible session.
- Additional boxes are background sessions with `syncProcess: true`.
- Each session owns its controller, entities, session blackboard, profile, and
  save payload.
- The session topology is the authoritative list of live boxes.
- Box profile is stored on the session blackboard so load can rebuild the
  runtime before strategies run.

### 3. Migration keeps traits stable

A migrant keeps genome, phenotype, energy, age, and root lineage. It is not
recompiled against the destination profile.

Compatibility rule:

> A creature may enter a box only if the destination profile allows every
> expressed gene/organ/interface of that creature. Otherwise migration is
> rejected with an explicit reason.

This prevents “profile leaks” (a restricted box silently running a locked
organ) and prevents “trait stripping” (the same genome behaving differently in
the destination). New mutations in the destination use the destination gate.

A 斗蛐蛐 arena is an all-enabled box with a manually seeded initial population.

### 4. Arena

An arena is a normal ecology box, not a separate simulation class:

- all-enabled profile;
- the initial population is seeded manually from selected creatures;
- normal reproduction, food, collision, migration, save, and analysis apply;
- a termination rule can be added later without changing the box model;
- there is no arena-specific analytics path; it is analyzed by `box.id` like
  any other box.

### 5. Analytics

Every save/metric must carry a box id. Per-box metrics reuse the existing root
birth/death/survival/hazard/event-rate reports. Cross-box metrics add
migration flows, origin box, and compatibility rejects.

## Migration stages

1. **Box-scoped content and gates** — done in this change:
   `GenomeContentGate` is an instance on `GameplayRuntime`; `BoxUnlockProfile`
   and `GameplayMode.Profile` exist; founders/mutations take a gate.
2. **Box registry and background sessions** — create/restore boxes as sessions,
   profile stored in session blackboard, strategies resolve the owning box.
3. **Migration** — compatibility checks, queue at frame boundaries, stable
   entity transfer, origin box metrics. The core transfer and trait passport
   are implemented; queued/UI-driven migration remains.
4. **Arena** — a manually seeded, all-enabled normal box. Termination rules
   remain deferred.
5. **UI and analytics migration** — box configuration, switch/foreground,
   migration controls, box-aware saves and reports. Save selection, metrics
   box identity, the box panel, pause/resume, and deletion are implemented;
   arbitrary profile editing and migration queues remain.

## Status

Stages 1–3 are implemented and tested.

- The foreground pond and a background meadow run as separate sessions.
- Each box owns a `GameplayRuntime`, `BoxUnlockProfile`, controller, and save
  payload; strategies resolve the owning box.
- The profile is stored on the session blackboard and mirrored on the
  controller so a load can rebuild the box before strategies process it.
- New-game and load reconcile the live session topology.
- `BoxMigration` applies the destination profile to every expressed trait,
  then transfers the stable genome/phenotype and life state without
  recompiling. `evo.rootOriginBox` preserves the origin namespace across boxes.
- The demo smoke requires two live boxes whose controllers both advance and
  migrates a creature across a save/load boundary.

The foreground UI exposes `G` box management, `B` switching, `N` creation,
`Shift+N` no-predation creation, and `M` selected-creature migration. The box
panel supports foreground switching, pause/resume, background deletion, and
seeding a selected creature into another box; arbitrary profile editing and
migration queues remain.
