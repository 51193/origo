<!-- docsync-pair: architecture/agent-friendly/openai-harness -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# OpenAI Harness Engineering and Concrete Gaps in Origo

> [↑ Back to the research index](README.en.md)

## Scope and evidence

This document is research and a proposal, investigated on 2026-09-18 against local baseline `cdba5e4`. **Facts** come from official material or local files; **inferences** describe possible effects; **proposals** have not been implemented or measured. The previous 7.5/10 assessment and projected 8.5–9/10 were not benchmark results and must not be treated as measurements.

OpenAI's 2026-02-11 [Harness engineering](https://openai.com/index/harness-engineering/) is a first-party account of an internal product team. It reports a short root entry point, structured repository knowledge, versioned complex plans, mechanical architecture constraints, and per-worktree application and observability environments. These mechanisms are useful subjects for investigation, but the article is not a publicly reproducible experiment. Its efficiency estimate cannot isolate any mechanism's effect or predict gains for a game framework.

## How a map works

A map must answer five questions: which domain owns the task; which authoritative entry to read next; which collaborators must be understood; how to verify the result; and how to locate failure. An encyclopedic entry point preloads answers for every task. A map supplies routing first, then task-specific detail, while retaining global constraints that must always be remembered.

Three layers have separate roles: root instructions provide persistent constraints and navigation; module documentation explains contracts and rationale; scripts and runtime tools expose current verifiable facts. Moving a long instruction manual to a link that must be read in full every time does not implement progressive disclosure. Shortening the entry without providing routing instead forces guessing and omissions.

**Inference:** Origo should optimize information retrieval cost while preserving or improving contract compliance. Reading fewer required collaborators, skipping history, or bypassing orchestration is not an acceptable speed improvement. The OpenAI case's merge-gate tradeoffs should also not be copied directly. Origo's strict persistence, lifecycle, and coverage requirements remain in force.

## Existing foundation and gaps

| Concern | Local fact | Gap and implication |
|---|---|---|
| Root entry | Root `AGENTS.md` requires complete META, release-process, and module-chain reading; it includes the release checklist, DocSync table, dependency families, and buffer procedure | Routing and specialized procedures overlap; changing a console message still loads formal release operations |
| Knowledge navigation | The [usage index](../../usage/README.en.md) routes by reader; its Agent path points to the complete agent-reference | Reader routing exists, but task routing for strategy writing, click diagnosis, and persistence repair is missing |
| Collaboration contracts | [Architecture overview](../overview.en.md) defines Core/Adapter, I/O, and frame boundaries; [architecture tests](../../Origo.Core.Tests/Architecture.en.md) check access paths and engine isolation | This is a strength; these boundaries belong in the mandatory map, rather than becoming targets for removal |
| Runtime state | [ConsoleBridge](../../Origo.ConsoleBridge/README.en.md) offers local single-client TCP; [commands](../../usage/console-commands.en.md) support queries, invocation, and interaction | Agent control already has a foundation, but text commands and shared logs do not directly correlate each request with its result |
| Visual observation | [Godot Console](../../Origo.GodotAdapter/Console/README.en.md) exposes `camera_view`, `tree_debug`, and `press_button` | Projection does not detect occlusion; emitting Button signals cannot establish that real input and layout are clickable |
| Handoff | [META](../../META.en.md) defines untracked, single-book `_origo_local` | Local recovery is useful; durable decisions and progress do not accompany a clone, requiring a separate tracked artifact design |

## Three concrete tasks

### Add a damage and death strategy to a game

The current [Agent Reference](../../usage/agent-reference.en.md) combines blackboards, files, saves, sessions, state machines, and strategy examples. The decisive facts are that shared strategies cannot hold mutable per-entity state, Data owns that state, and death should use `entity.OwningSession.RequestKillEntity`, leaving end-of-frame work and hooks to the framework.

**Proposal:** Route the game-development entry directly to the entity model, lifecycle, strategy testing, and session material required by death contracts. The Agent first identifies related facilities and then reads their modules instead of guessing relevance within the entire reference. Cross-entity observation or load behavior must expand the route to those contracts. Engine maintenance and games consuming the engine need separate procedures: releasing Origo is not a mandatory step for a game project.

### “The button exists, but clicking it does not start the game”

`tree_debug` can establish a node path, and `press_button` can exercise behavior after the signal. However, [GodotAdapter usage notes](../../Origo.GodotAdapter/README.en.md) explain that full-screen Control mouse filtering can consume 3D input and that headless viewports have zero size. Emitting `Pressed` bypasses actual pointer coordinates, GUI consumption, and input phases.

**Proposal:** Distinguish signal-path, real GUI-input, and game-state checks in the verification matrix. Capture GUI screenshots, coordinates, before/after state, and build identity. Treat node visibility and player clickability as separate acceptance criteria. Work-directory isolation is only a beginning: ports, `user://` state, logs, and saves also need isolation to prevent concurrent Agents interfering.

### “Autosave succeeded, but restarting restores the wrong data”

`SaveRequests` in [strategy testing](../../usage/strategy-testing.en.md) records requests only; the documented harness does not support full background sessions or nodes. It cannot establish complete disk writes, consistent snapshots, or restart recovery.

**Proposal:** Separate evidence for requests, the complete save pipeline, and host recovery. Real-path regression must use public Save entry points and frame driving, rather than manually invoking internal flush operations to simulate completion. Runtime evidence should identify seed, frame number, save root, payload and build, and verify behavior and state after recovery. Define measurable assertions before deciding whether additional tool interfaces are needed.

## Learning order and acceptance

Establish task routing and separate reader entry points first, then reproducible and queryable task environments, and finally turn recurring failures into checks or documentation. Origo need not copy OpenAI's naming or directory layout. The essential capability is independent discovery and verification of relevant facts.

Select strategy writing, real clicking, persistence recovery, and Core maintenance tasks at minimum. Fix the Agent and budget, then compare the current and proposed entry points. Record files read and context volume, time to first correct verification, human hints, architecture violations, hidden-test results, and recovery consistency. Without those observations, do not claim that map-based guidance improves success. Detailed proposals follow in [root instruction research](root-instructions.en.md) and [skills research](skills.en.md).
