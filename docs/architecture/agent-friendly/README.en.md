<!-- docsync-pair: architecture/agent-friendly/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Origo Agent Friendly Investigation

> [↑ Back to Architecture](../README.en.md)

## Conclusion and scope

This investigation separates reliable maintenance of Origo from developers asking agents to implement, run, and validate games using Origo. The latter is the primary product objective. Origo is a platform-independent C# game framework with a Godot adapter; rendering, physics, and the editor remain Godot responsibilities. Framework usability is not equivalent to a complete engine.

Value should appear as greater completion, less human intervention, and faster gameplay verification. Documentation length, skill count, MCP tool count, and coverage do not independently establish value. The earlier 7.5/10 was a subjective engineering assessment, not a benchmark result. This report publishes no unmeasured friendliness ranking.

Report date: 2026-09-18. Origo observation baseline: `cdba5e4`. Pin external commits, datasets, models, and harnesses before experiments. Reports describe findings and candidate designs, without changing current AGENTS gates, public APIs, or CI.

## Reading map

| User question | Report | Expected answer |
|---|---|---|
| Which existing benchmarks can we use? | [Benchmarks and experiment protocol](benchmarks.en.md) | Reusable evaluations, language/version constraints, controlled measurement of Origo changes |
| How can we learn from OpenAI; what exactly differs? | [OpenAI Harness comparison](openai-harness.en.md) | Evidence limits, concrete task gaps, applicable mechanisms |
| How should root instructions shrink? | [Root instructions and context routing](root-instructions.en.md) | Persistent constraints, conditional reading, budgets, acceptance |
| How should on-demand skills work? | [On-demand workflows](skills.en.md) | Triggers, input/output contracts, script boundaries, maintenance costs |
| How should affected checks work? | [Validation by impact](affected-checks.en.md) | Dependency closure, fast feedback, missed selections, final full gates |
| How can we generate API inventory? | [Machine API inventory](api-inventory.en.md) | Effective compiled surface, generated code, queries, human rationale |
| Does this serve AI game development? | [Game development value and direction](game-development.en.md) | Creation loops, hypotheses, competitive boundaries, falsifiable tests |

Read product value and benchmarks first, then the OpenAI comparison. Select engineering topics by observed failure categories. Each report independently covers observations, proposals, risks, and acceptance.

## Two paths should not collapse into one score

| Path | Tasks | Completion evidence | Contribution of the four proposals |
|---|---|---|---|
| Framework maintenance | Save pipeline changes, ordering regressions, generator extensions | Real-path regression, collaboration contracts, complete CI | Instructions, skills, affected checks directly improve maintenance |
| Building games | Pickups, damage, save restoration, playable loops | Startup, real input, rendering, state assertions, human gameplay judgment | Consumer routing, strategy templates, API inventory help directly; framework CI acceleration is less direct |

Game developers should not execute Origo's complete maintenance workflow. Their entry covers package versions, strategies/data, host startup, and their game's tests/runtime tools. Maintenance retains history, collaboration reading, DocSync, release, and architecture gates.

## Verified facts and corrections

- [AGENTS.md](../../../AGENTS.md) supplies one authoritative entry, collaboration reading, and completion gates. Its guard independently measured 223 lines and 16,364 bytes. Origo owns the 16 KiB budget; the remaining 20 bytes are neither a model limit nor a friendliness metric.
- `scripts/test.sh` tests the whole solution. Its `-m:1` addresses a Windows xUnit v3 discovery race. Improve scope selection rather than remove serialization without verification.
- [Snd role documentation](../../Origo.Core.Contracts/Abstractions/Snd/README.en.md) explicitly defines nine Snd roles plus `IStateMachineContext`, giving ten companions. Capability shorthand can confuse readers; it is not an established implementation defect.
- [TCP Bridge](../../Origo.ConsoleBridge/README.en.md) has configurable ports, with documented reasons for one client and loopback binding. Parallel tasks need isolated instances/state, not concurrent writes to one session.
- [Godot console](../../Origo.GodotAdapter/Console/README.en.md) `press_button` emits a button signal; `camera_view` reports projections. Neither establishes real input reachability or correct final rendering.
- [Real Godot integration tests](../../Origo.GodotAdapter.Integration.Tests/README.en.md) validate host contracts, not consumer games' visual, interaction, or gameplay acceptance.

## Limits of learning from OpenAI

OpenAI combines repository knowledge, executable constraints, and observable runtime environments into agent infrastructure. Its account is a practice report, not a market experiment for game frameworks. Origo can adopt mechanisms and validate them against failure traces; identical folders cannot establish popularity. [OpenAI Harness engineering](https://openai.com/index/harness-engineering/)

The four proposals are candidate interventions, not a mandatory bundle. Short instructions can lose constraints, skills can miss triggers, affected checks can miss validation, and inventory can add noise. Measure benefits and regressions separately, preserving fail-fast, one access path, and final full CI.

## Suggested sequence and stopping conditions

| Order | Deliverable | Verification and stopping condition |
|---|---|---|
| 1 | Runnable sample with pinned C# / Godot and a small hidden acceptance suite | Stabilize sample and grader before large evaluations |
| 2 | Baseline traces classified by context, API, startup, input, visual, validation | Do not blame every model failure on the framework; identify frequent tractable causes |
| 3 | Separate maintainer/consumer routing and one gameplay workflow | Repair routing when completion or compliance drops; fewer tokens are insufficient |
| 4 | Affected checks or minimal inventory chosen by failure evidence | Export queried symbols; fast checks do not claim full gate success |
| 5 | Isolated launch, real input, screenshots/video, state observation, reproduction artifacts | Stabilize timing, imports, and environment before judging visual failures |
| 6 | Developer trials against direct development on the same Godot base | Reconsider positioning when learning cost rises, completion does not improve, or users do not return |

The long-term direction is an agent-operable game system: specifications to strategies/assets, observation to deterministic reproduction, requirement changes to reliable acceptance. Investment should follow retention, playable output, and human repair cost.

## Non-goals

This work does not implement runners, skills, selectors, generators, or runtime services. Proposed commands are not existing CLIs. It changes no public interface or released version. Later implementation needs its own complete AGENTS loop.

Behavior/API test extensions and feature Changelog entries are inapplicable to research-only documentation. Bilingual synchronization, navigation, generated artifacts, post-commit CI, and message lint remain applicable. Recheck licensing/reproducibility against each report's source versions.

---
[↑ Back to Architecture](../README.en.md)
