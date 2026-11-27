# Origo Evolution Demo — Design and Playtest Docs

> [↑ Back to the demo README](../README.md)

This directory is the Evolution Demo's design notebook. It is deliberately
separate from the framework manual under `docs/`.

## What belongs here

- **Problem → Thinking → Decision → Result.** Start with the player experience
  or test problem, show the alternatives considered, record the decision, then
  record the evidence and what changed.
- **Playtest methods and baselines.** Which seeds, strategies, run lengths,
  checkpoints, and metrics produced a conclusion; where the archived baseline
  lives.
- **Player experience.** Pacing, readability, failure modes, progression
  friction, and the gap between the intended and observed experience.
- **Balance/content decisions.** Food economy, predation, enrichment, organ
  trade-offs, progression gates, and the evidence behind them.

## What does not belong here

- API, interface, file-list, or persistence-contract reference. Those live in
  the framework manual or the technical README beside the code.
- Framework rules, release gates, or architecture decisions. Link to `docs/`
  instead.
- Raw per-run artifacts. Full saves and logs stay in the ignored `.scratch/`
  workspace; only the compact, committed summary and its reproduction command
  belong here.

## Style

- Write for a designer or a future agent who has no memory of the last session.
- Prefer the question and the evidence over the implementation. "What did the
  player see?" matters more than "which method was called".
- Keep decisions dated and attributable to a commit or release tag.
- When a decision is superseded, keep the old result and replace it with a
  pointer; do not erase history.
- Every baseline record names its strategy, seeds, horizon, commit, and how to
  reproduce it.

## Index

| Topic | Entry |
|---|---|
| Campaign player-model baselines | [campaign-baselines](campaign-baselines.md) |
| Six-seed strategy matrix | [campaign-matrix-2026-11-25](campaign-matrix-2026-11-25.md) |
| Enrichment × predation controls | [campaign-controls-2026-11-26](campaign-controls-2026-11-26.md) |
| Root lineage and mortality, 10 seeds | [campaign-rooted-2026-11-27](campaign-rooted-2026-11-27.md) |
| Root × death cause | [campaign-root-mortality-2026-11-27](campaign-root-mortality-2026-11-27.md) |
| Genome v4 design baseline | [GENOME_V4_DESIGN](../GENOME_V4_DESIGN.md) |
| Content pack notes | [Content](../Content/README.md) |
| Genome v4 implementation reference | [Logic/V4](../../Origo.EvolutionDemo.Logic/V4/README.md) |
| Analysis tool reference | [Analysis](../../Origo.EvolutionDemo.Analysis/README.md) |

## Baseline archive

Campaign baselines are stored under [`baselines/`](baselines/). The matching
Markdown chapter explains why those strategies were selected; the JSON file is
the compact machine-readable record (strategy, seed, purchase timeline, progress,
ecology and performance metrics) used by later comparisons.

## Boundary with the framework manual

The framework manual `docs/` may link here, but it must not absorb this content.
`docs/` explains how Origo works; this directory explains what the Evolution
Demo is trying to make the player feel, what was tested, and what was learned.
When both change in one pull request, keep each kind of prose in its own home.

## Audit note (2026-11-25)

Before this directory was created, a repository audit found no demo design or
playtest documents under `docs/`: the framework index and usage page only linked
to `samples/`, and `docs/maintenance-log.en.md` contains repository-maintenance
entries rather than demo design prose. The demo's existing design material was
already under `samples/` (`GENOME_V4_DESIGN.md`, `Content/README.md`, and
`Logic/V4/README.md`). The pull-request history for #155/#156 refreshed demo
technical READMEs and framework index links; neither moved demo design docs
under `docs/`. This directory now makes the design/playtest boundary explicit;
new demo design records belong here, not in `docs/`.
