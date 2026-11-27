# Root Lineage and Mortality — 2026-11-27

> [↑ Back to demo Docs](README.md)

## Problem

The previous lineage analysis used `evo.lineage`, which the game rewrites when a
child accumulates enough mutations. That made root ancestry invisible. The
2×2 control also had only aggregate starvation/predation/aging counters, not a
stable-lineage view of the same run.

This follow-up uses the new `evo.rootLineage`, `evo.spawnOrigin`, and death
counters on a fresh planned-seed batch.

## Decision

- 10 fresh planned seeds: 20261401–20261410.
- 3,600 s, 300 s checkpoints, fresh progression, normal planned policy.
- Analyze 1,500 s, 1,800 s, and final together so root/death counters span the
  late campaign.
- Compact data:
  [campaign-rooted-2026-11-27.json](baselines/campaign-rooted-2026-11-27.json).

## Result

| Group | Count | Final population | Root lineages | Enrichment founders | Predation share | Starvation share |
|---|---:|---:|---:|---:|---:|---:|
| high survivor (>1,000) | 2/10 | 1,546 / 1,603 | 3–5 | 0 | 16–23% | 69–80% |
| low end | 8/10 | 267–447 | 81–122 | 40–71 | 37–59% | 38–60% |

### High survivors

The two high survivors never bought enrichment and had **3–5 root lineages**.
Their mortality was mostly starvation (69–80%); predation was only 16–23%.

This matches the earlier observation that the automatic planned policy can
stall in the `enrichment off, predation on` regime when point income never
reaches the enrichment purchase. The high population is not founder-diverse:
it is a small number of roots with a large mutation-lineage cloud (1,061–1,086
mutation ids).

### Low end

All eight low runs bought enrichment (40–71 living founders at final) and kept
**81–122 root lineages**. Predation and starvation were both substantial
(37–59% and 38–60%). The higher root diversity coexisted with the lower
population.

### Combined frequency

Adding the earlier 29 planned runs gives six high survivors in 39 runs
(~15%). The instrumented batch alone is 2/10 high.

### What the death counters add

The low group's predation share is **37–59%** of tracked deaths; the high group
is **16–23%**. The low group also has a larger root pool and enrichment
injection. This is consistent with the 2×2 result: enrichment supplies many
roots, predation removes them, and the combination ends in the low-density
regime. The counters now make the death mix explicit instead of estimated from
`births − population`, and they do not depend on the enrichment-birth
undercount.

## Caveats

- Planned auto did not randomize the enrichment choice; the high group is a
  point-income selection effect. The controlled 2×2 remains the causal test.
- The high group has only two seeds in this batch.
- Root lineage is stable ancestry but not a full parent tree.
- Death counters are cumulative aggregate counts, not per-individual causes.
- `starvation` means energy reached zero; `aging` means maximum age was reached;
  `predation` means a lethal spike hit. A death is counted once.

## Reproduce

```bash
ROOT=/path/to/origo
SEED=20261404

EVOLUTION_DEMO_SMOKE_HOME=/tmp/rooted-$SEED \
ORIGO_DEMO_SMOKE_SEED=$SEED \
ORIGO_DEMO_CAMPAIGN_STRATEGY=planned \
ORIGO_DEMO_CAMPAIGN_FRESH=1 \
ORIGO_DEMO_CAMPAIGN_PURCHASE_INTERVAL=10 \
  bash "$ROOT/samples/Origo.EvolutionDemo/run-demo.sh" --no-build --headless \
  --seconds 2 --fast-forward 3600 --checkpoint-interval 300 --save-slot final
```

Analyze root ancestry and mortality:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  genome --save <saves>/save_iterate_1500 \
  --save <saves>/save_iterate_1800 --save <saves>/save_final \
  --root-lineages-csv /tmp/root-lineages.csv \
  --metrics-json /tmp/rooted.metrics.json
```
