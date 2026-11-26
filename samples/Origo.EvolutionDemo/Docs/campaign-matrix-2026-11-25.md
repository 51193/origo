# Campaign Strategy Matrix — 2026-11-25

> [↑ Back to demo Docs](README.md)

## Problem

The first three-seed baseline left an ambiguous picture for the `planned`
strategy: two runs ended around 200–340, one ended at 1,663. That was enough to
say "the end state is bimodal", but not enough to know whether the high-density
outcome was a common mode or a rare survivor. The baseline also had only one
control run for `greedy`, so the shape of the difference between player models
was undersampled.

This matrix asks a narrower question: **with the same fresh-start test
environment, how much of the outcome is the spending strategy and how much is
the seed?**

## Thinking

- A single high run can be a rare survivor. Six seeds per strategy give a
  clearer frequency signal without changing the 3,600 s horizon.
- Final population alone hides the experience. A run that peaks at 1,600 and
  ends at 300 is not the same as one that stays near 300 throughout.
- The key path metrics are: 600/1,500/1,800/3,600 population, peak population,
  final/peak ratio, first/last purchase, enrichment purchase time, founder
  share, gene count, and final cluster shape.
- Seeds that fail the 2 s smoke generation check are not bad worlds; the smoke
  phase is only a save/load probe. Rerun those seeds with a 10 s smoke and keep
  the benchmark start fresh. The smoke length does not change the benchmark
  progression.

## Decision

- Four strategies: `planned`, `idle`, `cheapest`, `greedy`.
- Six seeds: 20261201–20261206.
- 3,600 simulation seconds, 300 s checkpoints, fresh profile, purchase log.
- 24 runs total. Seeds 20261201/02 were rerun with a 10 s smoke; the rest used
  the normal 2 s smoke. Every benchmark still starts from zero progression.
- Full data: [campaign-matrix-2026-11-25.json](baselines/campaign-matrix-2026-11-25.json).
- Analysis tool and game tree: merged `main` at 77466b55.

## Result

### Population path ranges across six seeds

| Strategy | 600 s | 1,500 s | 1,800 s | 3,600 s | Peak | Final/peak |
|---|---:|---:|---:|---:|---:|---:|
| planned | 837–899 | 1,376–1,599 | 1,484–1,599 | **262–361** | 1,484–1,992 | **0.13–0.24** |
| idle | 108–120 | 119–120 | 120–250 | 274–333 | 290–353 | 0.84–1.00 |
| cheapest | 120–250 | 166–238 | 134–295 | 260–466 | 280–466 | 0.78–1.00 |
| greedy | 325–394 | 658–723 | 161–236 | 264–474 | 658–723 | 0.40–0.68 |

### Final envelope and content state

| Strategy | Final pop | Founder | Gene count | Enrichment purchase | Predation last interval |
|---|---:|---:|---:|---:|---:|
| planned | 262–361 | 10.5–18.7% | 47.5–59.1 | 2,056–3,317 s | 78–106% |
| idle | 274–333 | 12.4–18.6% | 50.3–57.9 | 600 s | 101–106% |
| cheapest | 260–466 | 7.2–15.4% | 51.2–80.2 | 211–632 s | 97–108% |
| greedy | 264–474 | 10.7–20.8% | 51.9–61.9 | 37–73 s | 98–105% |

Final clusters still do not collapse to one shape: k=2–8, silhouette
0.15–0.51. The only strong two-cluster solution is one cheapest seed
(k=2, silhouette 0.51); most are weak or use the eight-cluster cap.

## What changed in our understanding

### 1. The planned strategy reliably builds a high-density state, but it rarely keeps it

All six planned runs reached roughly 1,400–2,000 by 1,800 s and all six ended
at 262–361. Combined with the three baseline seeds, the planned outcomes are:

```text
201, 262, 312, 328, 335, 341, 355, 361, 1,663
```

Only **one of nine** runs held the high-density state. The high outcome is
real, but it is a rare survivor rather than a representative planned result.
The earlier "bimodal" wording was directionally correct, but the matrix shows
that the low mode is strongly preferred over 3,600 s.

A later 20-seed survival run found three more high survivors; combining all
29 planned runs gives four high outcomes. The controlled follow-up is
[campaign-controls-2026-11-26](campaign-controls-2026-11-26.md).

### 2. The matrix alone could not identify the enrichment trigger

Every matrix planned run bought enrichment late (2,056–3,317 s) and every one
collapsed. The one high-density planned run in the earlier baseline never
bought enrichment, kept founder share at 0, and reached 64 gene copies.

Idle, cheapest, and greedy buy enrichment much earlier (600 s, 211–632 s,
37–73 s), but they do not show the same planned peak-and-collapse shape. The
matrix is observational: the enrichment purchase is often after the 1,800 s
peak, and seed, founder lineage, point income, and predation move together.
It cannot separate the causes.

The controlled follow-up does: removing enrichment **or** removing the
predation family lifts the same four seeds from 294–458 to 1,916–2,498 at
3,600 s. Enrichment and predation are both necessary; the interaction is the
collapse condition. See
[campaign-controls-2026-11-26](campaign-controls-2026-11-26.md).

### 3. The player model changes the shape more than the final median

At 3,600 s the final ranges overlap: planned 262–361, idle 274–333, cheapest
260–466, greedy 264–474. The median planned result (331.5) is not dramatically
different from idle (300.5).

What differs is the path:

- **planned** builds ~1,500–2,000, then loses 76–87% of the peak;
- **idle** stays near 300 and barely moves;
- **cheapest** grows late to 260–466 and rarely crashes;
- **greedy** peaks near 700, crashes to ~200 at 1,800 s, then recovers to
  264–474.

A baseline that records only the final population will miss the most important
player experience difference. The matrix baseline therefore retains the full
trajectory and peak/final ratio.

### 4. The cheapest and greedy strategies are not "low-density" in general

The earlier three-seed baseline put cheapest at 245–282 and greedy at 259.
With six new seeds, cheapest reached 260–466 and greedy 264–474. Greedy's
1,800 s crash is consistent (161–236), but its later recovery varies from
264 to 474. Cheapest can grow a large late population (466) and one run reached
80 gene copies. "Cheapest" means spending style, not a fixed ecology.

### 5. The earlier baseline is still useful, but its planned envelope is too optimistic

The [campaign-baselines](campaign-baselines.md) document remains the record of
the first three-seed run. Its planned 1,663 case is a real but rare high
survivor; do not use its upper bound as the expected planned outcome. Use the
matrix path envelope for planned, and treat the high-density case as an
experiment to reproduce rather than a baseline expectation.

## Reproduce

```bash
ROOT=/path/to/origo
EVOLUTION_DEMO_SMOKE_HOME=/tmp/matrix-planned-20261201 \
ORIGO_DEMO_SMOKE_SEED=20261201 \
ORIGO_DEMO_CAMPAIGN_STRATEGY=planned \
ORIGO_DEMO_CAMPAIGN_FRESH=1 \
ORIGO_DEMO_CAMPAIGN_PURCHASE_INTERVAL=10 \
ORIGO_DEMO_CAMPAIGN_PURCHASE_LOG=/tmp/matrix-planned-20261201.csv \
  bash "$ROOT/samples/Origo.EvolutionDemo/run-demo.sh" --no-build --headless \
  --seconds 2 --fast-forward 3600 --checkpoint-interval 300 --save-slot final
```

For seeds 20261201/02 use `--seconds 10` if the 2 s smoke does not reach
generation 1. The benchmark reset makes the smoke length irrelevant to the
reported run.

Analyze a checkpoint interval with the analysis tool:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  genome --save <saves>/save_iterate_3300 --save <saves>/save_final \
  --timeline-csv /tmp/matrix.timeline.csv --metrics-json /tmp/matrix.metrics.json
```

## Caveats

- 24 runs is enough to overturn a "representative high survivor" reading, but
  not enough to estimate the rare high outcome precisely. A 20+ seed planned
  matrix should follow before making probability claims.
- Enrichment timing is observational. Seed, founder count, food, and predation
  timing all move together; only the controlled 2×2 separates them.
- Predation share is an upper bound whenever enrichment is active (the death
  estimate is a lower bound). Every matrix run bought enrichment; the earlier
  high planned control did not.
- The 2 s smoke failure is a test-startup constraint, not a world-quality
  judgment; the 10 s rerun is noted in the JSON per run.
