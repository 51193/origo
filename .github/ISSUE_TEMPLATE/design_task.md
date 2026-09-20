---
name: Design-Gated Task
about: Track deferred work that needs a design decision or a re-evaluation signal
title: ""
labels: ""
assignees: ""
---

> Use this template for memo-level work that is intentionally not ready to
> start: a design decision is missing, a product signal has not arrived, or the
> task depends on an earlier architecture change. Use the Feature Request
> template for work that is already ready to build.

## Status / gate

- **State:** blocked-design / waiting-signal
- **Decision owner:** @51193
- **Blocked by:** #...
- **Re-evaluation signal:** <!-- The observable condition that turns this into ready work. -->
- **Milestone:** none until the signal is met and the work is scheduled.

## Context / current state

<!-- Verified current behavior, commands, files, commits, and related issues.
     Do not record speculation as fact. -->

## Problem

<!-- What problem or use case? Who is affected? Why do the existing
     capabilities not cover it? -->

## Goal / non-goals

<!-- What must this change achieve? What is explicitly out of scope? -->

## Design decisions needed

| # | Decision | Recommended default | Why |
|---|----------|---------------------|-----|
| 1 |  |  |  |

## Proposed approach

<!-- A direction, not a binding final design. Call out effects on fail-fast
     behavior, single access paths, persistence, and compatibility. -->

## Acceptance criteria

- [ ] Behavior or API result is observable and tested through a real path.
- [ ] Bilingual documentation and DocSync are updated, or the exemption is stated.
- [ ] `CHANGELOG.md` is updated when the change is user-visible.
- [ ] `scripts/test.sh`, post-commit `scripts/ci.sh`, and
      `scripts/lint-commits.sh` pass when the task is implemented.
- [ ] No compatibility shim or evolution marker is introduced
      (`AGENTS.md §1.2`).

## Evidence / verification

<!-- Commands, existing tests, relevant documentation, and git history. -->

## Alternatives considered

<!-- Optional. What other approaches were rejected, and why? -->

## Dependencies / related

<!-- Native blocked-by / parent / sub-issue links where available; otherwise
     state the relationship explicitly. -->
