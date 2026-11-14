# Evolution Demo Analysis Tools — Delivery Report

Status: implemented, locally validated, not part of repository CI.

## 1. What was delivered

New project:

```text
samples/Origo.EvolutionDemo.Analysis
```

Two commands:

```text
genome   current v6 save -> feature matrix -> PCA/k-means -> HTML report
perf     pipeline profile JSONL -> phase/percentile report -> HTML report
```

Detailed usage: [README.md](README.md).

The project is intentionally outside `Origo.sln` and CI. It is part of the
playable Evolution Demo sample and is run locally.

## 2. Genome analysis

Command:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  genome \
  --save <save-root-or-snd_scene.json> \
  --out genome-report.html \
  --max 5000 \
  --seed 1 \
  --k 0
```

Implemented with no third-party math dependency:

- v6 SND save reader;
- V4 genome decode + phenotype compile + neutral brain evaluation;
- structural, body, neural, energy, port, and behavior features;
- median imputation and z-score standardization;
- power-iteration PCA;
- k-means++ with multiple restarts;
- silhouette-based automatic k;
- cluster profile and top differentiating features;
- self-contained HTML + cluster CSV.

Validation data:

```text
save:         save_analysis_no_profile (v6)
creatures:    1173
k:            3 (auto)
silhouette:   0.422
clusters:     1164 / 6 / 3
```

Important: this clustering is **outlier-sensitive**. One large cluster contains
most creatures; the small clusters are behaviorally distinct but too small for a
population conclusion. The extractor, standardization, PCA, k-means, report, and
CSV are exercised end-to-end, but feature weighting/gene-set distance still need
a second pass before using cluster labels as biological evidence.

Initial observations from the report:

- the large cluster is low-drive and low-displacement;
- the small high-drive cluster has high spike drive, more organs, and higher
  displacement;
- neutral spike output is the dominant separator;
- a tiny third cluster is defined mostly by fewer neural outputs and lower
  maintenance.

These are tool validation signals, not a final claim about the ecosystem.

## 3. Performance analysis

Profiling is disabled by default. Enable:

```bash
ORIGO_DEMO_PROFILE_PATH="$PWD/.scratch/profile.jsonl" \
ORIGO_DEMO_PROFILE_INTERVAL=10 \
bash scripts/bash/run-evolution-demo.sh --headless --seconds 5 --fast-forward 120
```

Generate report:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  perf --profile .scratch/profile.jsonl --out perf-report.html
```

Measured profile:

```text
samples:            126
sim range:          1.0 .. 119.8
population range:   886 .. 1171
food range:         2091 .. 2507
wall mean:          24.02 ms
wall P50/P95/P99:   24.00 / 29.18 / 31.09 ms
alloc mean:         23.2 MiB
alloc P95:          27.5 MiB
GC 0/1/2:           176 / 13 / 0
```

Phase breakdown:

| system | mean ms | P95 ms | share |
|---|---:|---:|---:|
| evolution.core | 19.616 | 24.075 | 81.7% |
| evolution.snapshot | 3.349 | 3.914 | 13.9% |
| evolution.foodsource | 1.019 | 1.307 | 4.2% |
| evolution.corpse | 0.018 | 0.068 | 0.1% |
| evolution.commit | 0.007 | 0.009 | 0.0% |
| evolution.progression | 0.002 | 0.003 | 0.0% |
| evolution.foodbloom | 0.002 | 0.002 | 0.0% |
| evolution.collision | 0.000 | 0.001 | 0.0% |

This already gives the optimization target:

1. `evolution.core` is the dominant cost by far.
2. `evolution.snapshot` is the second target, and it is also allocation-heavy.
3. Food source/progression/collision are negligible in this default scenario.
4. Allocation pressure is high enough to justify allocation-focused profiling
   before micro-optimizing arithmetic.

## 4. Verification

```text
analysis project build:       passed
genome report:                1173 creatures, generated HTML + CSV
perf report:                  126 samples, generated HTML
game build/test:              95 tests passed
no-profile 120s smoke:        DEMO_BENCHMARK_OK, unchanged behavior
profile run:                  DEMO_BENCHMARK_OK with profiler enabled
```

## 5. Known limits

- Only current v6 saves are accepted.
- Single snapshot behavior features are a point-in-time view.
- `neutral.*` outputs use zero sensor input and are not closed-loop behavior.
- k-means auto-k can select a large outlier-dominant cluster; Ward/DBSCAN and
  gene-set Jaccard are not implemented yet.
- Profiling samples pipeline time and per-tick allocation, not full CPU stack
  samples; `dotnet-trace` integration is a future optional step.
- The profiler is env-gated and writes JSONL; do not leave it enabled during
  normal play.

## 6. Recommended next work

1. Add feature-family weighting and gene-set Jaccard distance, then re-run
   clustering on multiple time points.
2. Add Ward and DBSCAN to separate stable clusters from outliers.
3. Add cluster time-series and gene-frequency drift.
4. Add allocation attribution in `evolution.core` and `evolution.snapshot`.
5. Add a small scenario matrix (population/food/patches/collision) to compare
   phase cost under load.
