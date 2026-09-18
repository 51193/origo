<!-- docsync-pair: architecture/agent-friendly/benchmarks -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Evidence and an Experimental Protocol for Agent Friendliness

> [↑ Back to the agent-friendliness study](README.en.md)

Research date: 2026-09-18. This page reports research and proposes experiments. Origo has not run these experiments; target metrics are not measured results.

## What existing evaluations measure

This search of primary sources found no universally accepted certification of repository agent friendliness. Task success depends on the model, harness, budget, task difficulty, and repository together. Rankings across different projects cannot isolate repository design. The following tools offer useful methods, rather than direct scores for Origo.

| Evaluation and date | Scope, languages, and limits | License and operating cost |
|---|---|---|
| SWE-bench, ICLR 2024; Verified released 2024-08 | Real issue resolution; Verified contains 500 tasks. Original tasks primarily use Python. Multilingual contains 300 tasks from 42 repositories in nine languages, excluding C# and GDScript. Hidden tests and regression checks are useful precedents.[Dataset guide](https://github.com/SWE-bench/SWE-bench/blob/main/docs/guides/datasets.md), [Multilingual](https://www.swebench.com/multilingual.html) | MIT harness; the Multilingual dataset card specifies MIT, while target repositories retain their licenses. Docker builds, tests, and inference incur separate costs. Recommended hardware: x86_64, 120GB free storage, 16GB RAM, eight cores.[License](https://github.com/SWE-bench/SWE-bench/blob/main/LICENSE), [dataset card](https://huggingface.co/datasets/SWE-bench/SWE-bench_Multilingual), [execution](https://github.com/SWE-bench/SWE-bench#-usage) |
| Terminal-Bench 2.0, 2026-01 paper | 89 containerized terminal tasks, extending beyond maintenance. Useful for tool use and environment recovery, without a standardized Origo/C# game suite.[Paper](https://arxiv.org/abs/2601.11868) | Apache-2.0. Harbor supports local Docker; `harbor run --dataset terminal-bench@2.0 --agent oracle` checks reference solutions. Costs include container resources, timeouts, and inference.[Repository and commands](https://github.com/harbor-framework/terminal-bench-2), [license](https://github.com/harbor-framework/terminal-bench-2/blob/main/LICENSE) |
| RepoBench, ICLR 2024; v1.1 updated 2024-02 | Cross-file retrieval and completion in Python/Java, without full repair, game execution, or delivery assessment. Useful for context retrieval analysis, insufficient for task evaluation.[Official description](https://github.com/Leolty/repobench) | Repository LICENSE is CC BY 4.0; underlying project rights require separate checks. Local Transformers inference needs appropriate compute; downloading data is not the total execution cost.[License](https://github.com/Leolty/repobench/blob/main/LICENSE) |

See the [game-development report](game-development.en.md) for direct evidence: GameDevBench supports reproducible localized Godot edits; GameCraft-Bench has a public repository for complete game delivery; JamBench describes generation/completion, but this search did not verify a downloadable full release and consolidated dataset license. These evaluate development artifacts. Playing an existing game evaluates a different capability.

## Lessons from AGENTS.md ablations

The 2026-02 paper [Evaluating AGENTS.md](https://arxiv.org/html/2602.11988v1) compares absent, generated, and developer-written context. Its AGENTbench contains 138 tasks across 12 Python repositories, alongside SWE-bench Lite. Generated files reduce success in five of eight settings, increasing average inference cost by approximately 20%/23%. Developer files usually help slightly while raising cost; Claude Code is the exception to success improvement. This does not establish that all AGENTS.md files are harmful or measure Origo's instructions.

Inference: Origo should evaluate relevance and reading cost rather than maximize instruction length. Existing gates still apply. Ablations require isolated experimental copies and predefined changes to explanatory material, with identical safety and production contracts. Ordinary work must not bypass repository rules.

## Minimal path to a first run

These documented commands require each project's prerequisites and were not run in this investigation. Pin repository SHA and Godot/Python/system dependencies, then validate original reference solutions before deriving C# tasks. Replace `<task>` with an actual directory under the pinned repository's `tasks/`.

**GameDevBench:** prepare exact Godot 4.4.1, uv, Python 3.10+, and the documented Linux Bubblewrap/Xvfb/xauth dependencies. Reference validation uses `validate_tasks.py`; do not invent an `--agent oracle` option.[Official setup and validation](https://github.com/waynchi/gamedevbench#verify-your-setup)

```bash
git clone https://github.com/waynchi/gamedevbench.git
cd gamedevbench
bash unzip_tasks.sh
uv run python validate_tasks.py
```

Validation checks all reference solutions in parallel, requiring resource planning. The official model entry is `uv run python gamedevbench/src/benchmark_runner.py --agent AGENT --model MODEL run --task-list tasks.yaml`; replace the installed agent and exact model ID.

**GameCraft-Bench:** follow its installation section for Ubuntu 22.04 display/recording dependencies, Godot 4.6.2, uv, and Python 3.12. Configure engine paths and judge credentials in `.env`, and provide licensed assets for selected tasks. These commands validate reference and empty submissions; reference evaluation may still invoke a paid judge.[Official installation and task commands](https://github.com/FreedomIntelligence/gamecraft-bench#install)

```bash
git clone https://github.com/FreedomIntelligence/gamecraft-bench.git
cd gamecraft-bench
uv venv --python 3.12 .venv
source .venv/bin/activate
uv pip install -e .
cp .env.example .env
# Configure .env and assets/system dependencies first; replace <task>
./scripts/run.sh -p tasks/<task> --agent oracle
./scripts/run.sh -p tasks/<task> --agent nop
```

**Terminal-Bench 2.0:** prepare uv and working Docker. Harbor automatically downloads the named dataset, avoiding an assumed source-repository runner.[Official commands](https://github.com/harbor-framework/terminal-bench-2#getting-started)

```bash
uv tool install harbor
uv run harbor run --dataset terminal-bench@2.0 --agent oracle --n-concurrent 4
```

After reference acceptance, empty-submission rejection, isolation, and logging checks, create a separate Godot.NET/C# derivative with the necessary engine, build, and verification changes; validate its references again. Give derivatives separate names and versions, never reporting their scores as original-suite results. Design maintenance hidden tests and game input/visual acceptance below; these tools do not yet supply an Origo runner.

## Proposed executable Origo A/B protocol

Start with **four tasks: two maintenance and two game tasks, three trials per condition, totaling 24 runs**. This checks harness operation, cost, isolation, grading, and failure classification only; it cannot establish significant improvement or market value. The 12 tasks per track and five trials per condition below total 240 runs, a suggested formal-study scale after the small pilot. Calibrate sample size using pilot variance and the effect size the study needs to detect.

1. **Freeze the manifest.** A uses a selected baseline commit and its current documentation. B adds candidate navigation, task recipes, or environment fixes. Separate code refactoring from documentation interventions. Record commit SHA, OS image, SDK/Godot versions, exact model ID, harness version, tool permissions, network scope, reasoning effort, and context-compaction policy. Use identical prompts, without steering B toward the correct files.
2. **Separate two tracks.** Start with 12 maintenance tasks spanning startup failure, strategy registration order, and observer save/reload contracts. Use private new defects or explicitly labeled fault injections; do not describe resolved problems as current defects. Start with 12 game-creation tasks in Godot.NET projects consuming Origo: template spawning, state-driven UI, and score recovery after saving. Report tracks separately so maintenance scores cannot conceal game-production failures.[Startup contracts](../../Origo.GodotAdapter/Bootstrap/README.en.md), [quick start](../../usage/quick-start.en.md)
3. **Fix budgets and repeat.** Run each task at least five times per condition, independently, in randomized order with fresh conversations and directories. Pilot budgets: 30 minutes for maintenance, 60 minutes for game creation, plus equal token/cost ceilings. Calibrate and freeze budgets before evaluation; never add retries selectively. Publish pass@1, all-run distributions, paired success differences, and confidence intervals clustered by task. Do not report only the best attempt.
4. **Grade independently.** Public smoke tests support iteration. Keep hidden tests, reference patches, and grading rules outside the solver environment. Maintenance requires target behavior and unchanged regression tests. Games additionally require actual mouse/keyboard input, screenshots or video at fixed resolution, and checks for readable UI, occlusion, focus, visible state feedback, and a complete gameplay loop. Use a predefined visual rubric and two blinded reviewers. Calibrate model judges against humans; a model judge must not be the sole acceptance authority.[Existing headless tests](../../Origo.GodotAdapter.Integration.Tests/README.en.md)
5. **Record cost and failure.** Report cold/warm startup time, time to first meaningful test, tokens, cached tokens, billed cost, CPU/GPU time, and human rescues. Classify failures as environment startup, localization, contract misuse, code/resource build, logical regression, real input, visual presentation, missing deliverables, budget exhaustion, or grading infrastructure. Preserve the first blocker and subsequent causes. Do not silently discard infrastructure failures or label them model logic failures.
6. **Prevent leakage and support audit.** Solvers must not access reference solutions, hidden tests, previous results, or sibling trials. Grading has no model credentials or external network. Keep a private holdout excluded from tuning, splitting by task family; mark released tasks as potentially contaminated. Preserve sanitized traces, artifact hashes, input replays, reviewer rationale, and version manifests. Record asset licenses separately from evaluation-code licenses.

Existing `scripts/godot-test.sh` checks SDK pairing, a positive test count, and ObjectDB leaks, providing real-engine verification infrastructure without complete GUI gameplay coverage. The [adapter console](../../Origo.GodotAdapter/Console/README.en.md) implements `press_button` through `EmitSignal(Pressed)`, which cannot establish actual click reachability. `camera_view` emits projection metadata rather than screenshots. Run the pilot before deciding whether its success/cost evidence warrants expansion.
