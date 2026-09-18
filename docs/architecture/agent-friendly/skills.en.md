<!-- docsync-pair: architecture/agent-friendly/skills -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# On-Demand Skills: Triggers, Contracts, Script Boundaries, and Acceptance

> [↑ Back to the research index](README.en.md)

## Facts and scope

Investigation date: 2026-09-18; baseline: `cdba5e4`. This report proposes candidate skills without creating or installing them. `git ls-files .agents .codex` returns no tracked repository files. Personal and system skills available in this session are not Origo distribution artifacts.

[Official Build skills documentation](https://developers.openai.com/codex/skills/) describes progressive disclosure: names/descriptions are supplied first, with full instructions loaded when selected; activation can be explicit or matched through descriptions. Reusable skills package instructions, resources, and optional scripts. This supports on-demand context but does not guarantee correct automatic matching.

**Inference:** Skills can reduce repeated procedure text, but cannot fix ambiguous APIs or replace executable validation. For a game engine, prioritize workflows that produce running, verifiable game features. Engine-maintenance skills alone do not directly deliver the user's game-development value.

## Two skill audiences and trigger examples

| Candidate skill | Positive trigger | Negative trigger | Essential output |
|---|---|---|---|
| `origo-game-strategy` | “Implement health, damage, and death; test recovery” | Explain SND only; update engine release version only | Strategy/Data/template changes, contract chain, behavior and recovery evidence |
| `origo-game-debug` | “Locate failed clicks or movement and verify runtime results” | Fix a docs link; static review only | Build/seed/environment identity, reproduction, before/after state, GUI applicability |
| `origo-engine-regression` | A defined Core/Adapter bug needing real-path red-to-green | Speculative redesign of an unknown contract; read-only evaluation | Symptom, real path, original failing evidence, unchanged passing test, sibling paths |
| `origo-docsync` | Change mirrored API/file/design or usage docs | Search only; internal implementation without structural/design changes | Bilingual authority updates, generate/validate results, generated-file list |
| `origo-release` | Explicit formal release preparation/execution | Every source task; general Changelog editing | Version/metadata alignment and existing complete release evidence |

Descriptions should lead with task verbs and objects, adding exclusions that prevent likely confusion. “Use for all Origo work” attracts unrelated tasks; “use formal release whenever Save is mentioned” is too broad. Multiple skills can serve one task, but steps and gates must refer to a unique authority rather than conflicting completion definitions.

Every skill preserves full-chain understanding, history, single access paths, and fail-fast. Debugging must not manufacture success by invoking internal flush or lifecycle hooks; regression must reproduce business symptoms rather than assert only an internal method. When AGENTS requires maintainer confirmation of a design issue, skills cannot authorize speculation.

## Input and output contracts

For `origo-game-strategy`, inputs include the game repository and Origo version, functional acceptance criteria such as requesting death at zero hp, entity/strategy indexes and existing Data types, recovery semantics, target host, and allowed change scope. Read values that can be established from existing material; ask only about missing definitions that affect behavior. Never fill unknown strategy indexes, data keys, or save formats with defaults.

Output extends beyond code: authoritative documents and collaborators read; files changed; shared-strategy state and lifecycle-pair checks; real behavior assertions; persistence-recovery assertions; commands/status; unsupported scenarios and unresolved failures. Templates can help structure code but must not automatically initialize business state in `AfterLoad`: [lifecycle documentation](../../usage/strategy-lifecycle.en.md) explains that doing so overwrites saved state.

For `origo-game-debug`, inputs identify reproduction, build, seed/initial state, and available hosts. Output distinguishes simulated Button-signal success, real mouse-input success, and expected game state. When [strategy testing](../../usage/strategy-testing.en.md) cannot support nodes or complete multi-entity interaction, route to a host/integration scenario instead of treating an unsupported check as passed.

## Skills versus deterministic scripts

| Decisions appropriate for skill guidance | Deterministic work for scripts/tools |
|---|---|
| Determine whether evidence establishes a defect, design, or unknown; choose collaborators and real paths | Parse SDK, versions, paths, project dependencies, and test selection |
| Choose a no-node harness, complete Core host, or GUI scenario | Build, execute tests, collect exit states and artifacts |
| Interpret gameplay acceptance and failure meaning | DocSync generation/validation, formatting, commit lint |
| Expand reading according to failures | Collect frame/seed/save/build identities and validate command arguments |

Skills call existing authoritative scripts rather than duplicating bootstrap, build, test, or generation logic. Stable execution needs explicit inputs, nonzero failure status, and machine-readable results; skills supply interpretation and decisions. A skill is neither a test harness nor a tool-permission grant.

## Compression, failure, and cross-Agent support

Names and descriptions also consume context; more skills are not automatically better. Keep triggers, workflow, essential constraints, and the output contract in the main file. Place substantial examples and modes in references read conditionally, rather than requiring all references. Long-running tasks should persist baseline, read contracts, acceptance, and execution evidence. A compressed chat summary cannot establish that tests passed.

Misrouting can recover through explicit invocation or direct authoritative reading. Missing files, unavailable SDK/host, or unsupported interfaces must produce clear failure and next actions, without reducing validation standards. Agents without skill support should find the same authoritative documents and scripts through root routing. Losing a wrapper must not lose the engineering contract.

An open skill format does not imply universal directory discovery, matching, MCP dependency handling, or permission semantics. Origo distributions should identify supported Agents/versions, operating systems, hosts, tools, and dependencies, and validate each combination. Avoid promising one skill works on every Agent. Consumers need usage workflows and tool contracts; engine-maintenance skills need not be forced into game projects.

## Maintenance and experiments

Each skill needs an owner and authoritative sources, with minimal copied API signatures. Module/command changes must check descriptions, references, and output contracts. Duplicate names, personal overrides, and launch directories are discovery cases worth testing. Retain a small set of valuable skills and maintain them through path checks, routing evaluation, and real-task replay.

Pilot recurring, well-scoped strategy-development and regression workflows first. Test explicit calls, natural-language implicit calls, ambiguous requests, negatives, scope expansion, and unavailable hosts. Measure trigger precision/recall, route correctness, output completeness, and violations. Then compare A/B: existing documentation routes versus on-demand skills, fixing tasks/Agents/budgets. Independently evaluate the resulting game and hidden tests, alongside human intervention, context, and execution costs. Correct activation with incorrect gameplay is still failure.

Agree thresholds before the experiment: essential safety/lifecycle compliance cannot decrease; behavior pass rates cannot worsen; retrieval or verification time should improve meaningfully. Count ongoing skill maintenance time too. Migration dependencies appear in [root instruction research](root-instructions.en.md); why context and runtime evidence both matter appears in [OpenAI Harness research](openai-harness.en.md).
