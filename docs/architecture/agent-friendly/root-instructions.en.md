<!-- docsync-pair: architecture/agent-friendly/root-instructions -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Root Instructions and Task Routing Research

> [↑ Back to the research index](README.en.md)

## Facts and objective

Investigation date: 2026-09-18; baseline: `cdba5e4`. This proposal does not change current rules. Independent file reading and `python scripts/validate-agent-docs.py` both confirm 223 lines and 16364 bytes in root `AGENTS.md`. The guard specifies `MAX_LINES = 300` and `MAX_BYTES = 16 * 1024`, leaving 20 bytes. It also requires 11 fixed subsection headings and particular commands and keywords; text-only compression remains constrained by that structure.

This is not a 16 KiB Codex platform limit. [Official AGENTS documentation](https://developers.openai.com/codex/guides/agents-md/) describes instructions combined from repository root to the current working directory, with a default 32 KiB project-instruction budget. Nested instructions are not discovered simply because a file in that directory is being edited. Starting at the repository root does not guarantee a nested instruction file is active.

**Inference:** The local capacity limit makes restructuring reasonable, but 223 lines alone does not establish poor quality. Evaluate routing accuracy, contract compliance, and reading cost; 100 lines is not an acceptance criterion.

## Persistent constraints and on-demand procedures

Global constraints must retain clear, actionable meaning:

- One authoritative entry, user language, task scope, and permissions; resolve conflicting documents in the same change.
- Read module documentation, upstream/downstream collaborators, and related facilities before source; inspect file history before changes. Efficiency cannot reduce full-chain reading to the current file.
- Fail-fast and strict integrity; authoritative references for strategy state, lifecycle, and Core/Adapter boundaries.
- One access path; do not assemble lower-level operations that bypass hooks, validation, or resource lifecycle. Confirm the design of suspected backdoors.
- Clean breaking changes in early development; no compatibility shells or test-convenience interfaces.
- Bilingual DocSync, no hand-edited generated artifacts, English public XML comments, and no DocFX/Sandcastle.
- Authoritative `global.json` and correct bootstrap; no SDK downgrade or temporary environment replacement.
- Completion definition: ordered tests, Changelog alignment, docs synchronization, commit, post-commit full CI and message lint; buffer unfinished work.

Full dependency-family rationale, release metadata checklists, DocSync procedures, internal test-access exceptions, and buffer producer/consumer fields can live in authoritative documents or task skills. The root retains triggers and mandatory gates. This preserves constraints while making required reading explicit.

## Proposed routing matrix

| Task trigger | Starting material | Required chain expansion | Verification evidence |
|---|---|---|---|
| Read-only research/evaluation | Architecture and relevant task documentation | Contracts supporting conclusions; source when needed | Sources, baseline, facts/inferences/unknowns; no artificial production-test changes |
| Documentation edit | Complete META authority; relevant module | Referenced usage, interface, and test documents | Bilingual content, generate/validate, and existing completion loop; no behaviorless tests |
| Core defect | Target module; META; testing rules | Inputs, consumers, sibling paths, file history | Real-path red-to-green regression and sibling coverage; iteration and final gates |
| Public API/file structure change | Target and related modules; API consumers | Public whitelist, interface boundaries, mirror inventory | Behavior tests, BREAKING decision, interface/file lists, DocSync |
| Godot runtime/input issue | Adapter, host, Console, integration-test documentation | Core frame boundary, nodes, real input consumption | Current build; headless versus GUI applicability; state and visual evidence |
| Dependency update | Bootstrap and Dependabot rationale | Version coupling, generator/SDK/test runner | Coupled-family update and full gates; no isolated family member bump |
| Formal release | Complete release-process authority | Version, analyzer tracking, workflows, docs | verify-release, generated artifacts, post-commit CI/lint, tag sequence |
| Unfinished discovery/task recovery | Complete META buffer protocol; buffer root and relevant chapters | Baseline revalidation, facilities, history/commands | Ownership, status, failure, next action, final commit |

This is a proposal, not a waiver. Current full-reading and completion rules still apply. In particular, current AGENTS §0 requires full release-process reading. Routing it only to release tasks requires coordinated edits to AGENTS, both META languages, release-process, and the instruction guard. A skill cannot override them.

General Changelog categories and BREAKING decisions should be routed separately from formal release operations; ordinary source changes can still require Changelog rules. META cannot simply become optional either: docs writes, builds, and buffer operations each have required authoritative sections. Keep its complete authority and route precisely to sections instead of duplicating drifting summaries.

## A concrete route

Task: “Fix `entity_set_data` writing to an existing float key.” Start with [Console](../../Origo.Core.Kernel/Runtime/Console/README.en.md), its CommandHandlers documentation, and [command semantics](../../usage/console-commands.en.md). Continue to data-type and converter collaborators, the real command queue/frame driver, relevant tests, and history. Changing a numeric parser alone can miss the contract to preserve an existing value's type.

Root instructions should expose this chain. A command-testing skill should reproduce through the real queue and frame entry. Formal release operations do not help this diagnosis, while public API changes, Changelog alignment, and final CI can still apply. Affected checks shorten iteration without replacing completion gates.

## Implementation and acceptance

First inventory each rule against persistent guidance or routing, unique authority, and enforcement. Migrate rule by rule and verify that meaning survives without duplicate authorities. Preserve size, paths, critical gates, and prohibited-pattern checks when updating the guard. Replacing fixed-numbered heading checks with rule-coverage checks needs dedicated validation, rather than removing protection.

Exercise root and subdirectory launch, explicit skills, Agents without skill support, and scope expansion into persistence or Godot. Compare identical tasks, baselines, Agents, and budgets. Measure missed collaborators, violations, and reading volume/time before correct verification. A shorter entry that omits essential context fails. A smaller root with a bloated skill catalog that increases overall reading also needs correction.

The eventual deliverable should include root guidance, a routing matrix, directly readable authoritative links, guard changes, migration coverage, and A/B evidence. This report defines those artifacts only; implementation and scores are not claimed.
