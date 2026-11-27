# Campaign Enrichment × Predation Controls — 2026-11-26

> [↑ Back to demo Docs](README.md)

## Problem

The six-seed strategy matrix showed that every `planned` run that bought
enrichment collapsed, while the one high survivor never bought it. But the
matrix could not tell whether enrichment caused the collapse:

- all matrix planned runs bought enrichment late, after their 1,800 s peak;
- most collapses were already underway before the purchase;
- the high survivor also differed in seed, founder lineage, and point income.

A counterfactual was missing. The next test had to remove enrichment or
predation on the same high-risk seeds and compare the paths.

## Thinking

- Keep the planned capacity-first build and the 3,600 s horizon.
- Use seeds that normally buy both enrichment and predation and normally end
  low: 20261301, 20261302, 20261304, 20261305.
- The four cells are enrichment off/on × predation off/on:
  - off/off removes both mechanics;
  - off/on keeps predation but removes founder injection;
  - on/off keeps founder injection but removes spike predation;
  - on/on is the existing control run.
- The off conditions are counterfactuals, not player strategies. They still
  spend points through the normal gates; the test policy simply skips the
  disabled unlock family.
- Separately, run 20 new planned seeds with the normal policy to estimate how
  often the high survivor appears.

## Decision

- 20 fresh planned seeds: 20261301–20261320, automatic policy.
- Four selected seeds for the 2×2: 20261301/02/04/05.
- Enrichment and predation controlled with
  `ORIGO_DEMO_CAMPAIGN_ENRICHMENT=0/1` and
  `ORIGO_DEMO_CAMPAIGN_PREDATION=0/1`.
- 3,600 s, 300 s checkpoints, fresh progression; control on/on values reused
  from the survival run.
- Compact data:
  [campaign-controls-2026-11-26.json](baselines/campaign-controls-2026-11-26.json).

## Result

### Twenty planned seeds

| Group | Count | Final population | Founder | Gene count | Enrichment |
|---|---:|---:|---:|---:|---|
| high survivor (>1,000) | 3/20 | 1,487 / 1,679 / 1,796 | 0% | 61.9–66.6 | never bought |
| low end | 17/20 | 275–665 | 8.7–20.4% | 49.8–60.3 | bought at 1,995–3,197 s |

The 20-seed median is **400.5**; the mean is 584.5. Combining the earlier
planned runs gives 29 seeds, four high survivors, and a combined median of
361.

The high survivors are not a different purchase order: they buy the same
ponds and optional senses, but their point income is low enough that the
planned policy never reaches enrichment. That makes the 2×2 mandatory.

### Controlled 2×2

| Enrichment | Predation | Final population | Peak | Founder | Gene |
|---|---|---|---|---:|---:|---:|
| off | off | 2,495–2,498 | 2,495–2,498 | 0% | 46.0–53.7 |
| off | on | 1,916–2,496 | 1,916–2,496 | 0% | 52.0–62.6 |
| on | off | 2,451–2,497 | 2,451–2,497 | 13.1–13.8% | 42.7–47.2 |
| on | on (control) | **294–458** | 1,566–1,599 | 8.7–13.3% | 54.7–60.3 |

The interaction is clear on all four seeds:

- **Enrichment alone does not collapse the pond.** It injects founders and
  lowers gene count (42.7–47.2), but the population still fills the cap.
- **Predation alone does not collapse the pond.** Without enrichment there are
  no naive founders to inject; final population stays high (1,916–2,496).
- **Both together collapse it.** The control peaks near 1,600 and ends at
  294–458.

So enrichment is a **necessary but not sufficient** condition for the planned
collapse on these seeds. Predation is also necessary. The interaction, not
either mechanic alone, is the current explanation.

This also explains the earlier automatic-run high survivors: their point income
kept them in the off/on or off/off region, so they never entered the
enrichment-plus-predation regime.

### Lineage follow-up from the same saves

The analysis tool's new living-lineage metrics add a first lineage view. These
are surviving founders/injected lineages at the final save, not cumulative
births:

| Cell | Final living lineages | New gen0 lineages at final transition | Final gen0 share | Final population |
|---|---:|---:|---:|---:|
| enrichment off, predation on | 1,535 | 0 | 0% | 2,192 |
| enrichment on, predation off | 1,842 | 335 | 13.4% | 2,497 |
| enrichment on, predation on | 321 | 48 | 11.3% | 425 |

These values use seed 20261301 and the 1,500 s, 1,800 s, and final saves. The
`newGen0` column is the 1,800 s → final transition, not a cumulative count or a
final-save-only count. Running all 13 checkpoints gives 318 for the
enrichment-on/predation-off cell; running only the final save reports 0 because
the first save has no previous comparison.

The founder-injection signal is real but conditional: enrichment without
predation keeps **335** new gen0 lineages alive; adding predation leaves only
**48** and collapses the population/lineage pool. The lineage ids themselves
are mutation clusters, so the new/lost counts are not family survival; the
gen0 counts are the useful founder-pressure proxy.

### Root lineage and death-cause follow-up

A second run of the same seed/2x2 after adding `evo.rootLineage`,
`evo.spawnOrigin`, and the three death counters gives the stable ancestry and
cause view (1,500 / 1,800 / final saves):

| Cell | Final pop | Root lineages | Root gen0 lineages | Predation share | Starvation share |
|---|---:|---:|---:|---:|---:|
| enrichment off, predation off | 2,496 | 3 | 0 | 0% | 92% |
| enrichment off, predation on | 2,192 | 2 | 0 | 21% | 74% |
| enrichment on, predation off | 2,497 | 407 | 335 | 0% | 92% |
| enrichment on, predation on | 425 | 91 | 48 | 55% | 42% |

This is the cleanest mechanism evidence so far:

- predation-only leaves two root lineages and no founder injection;
- enrichment-only keeps **407** root lineages and fills the cap;
- both leaves only 48 injected root lineages and raises predation to **55%**
  of tracked deaths; the population collapses.

A 10-seed instrumented follow-up is recorded in
[campaign-rooted-2026-11-27](campaign-rooted-2026-11-27.md); the root × cause
follow-up is in
[campaign-root-mortality-2026-11-27](campaign-root-mortality-2026-11-27.md).

The root lineage view also shows why mutation-lineage counts were misleading:
the same e1p0 snapshot has 1,842 mutation lineages but only **407** roots, and
e1p1 has 321 mutation lineages but **91** roots.

### What this changes from the matrix page

The [matrix page](campaign-matrix-2026-11-25.md) correctly said the six-seed
matrix alone had no temporal evidence that enrichment started the fall. The
controlled counterfactual now goes further: **removing enrichment completely
prevents the collapse even with predation on**, and removing predation
prevents it even with enrichment on. The matrix wording should be read as
"not identified by the observational matrix", not "enrichment is irrelevant".

## Mechanism hypothesis

The most economical hypothesis is a founder-pressure loop:

1. enrichment inserts generation-0 founders that were not shaped by the
   current pond;
2. predation removes experienced adults and rewards defensive traits;
3. repeated founder injection dilutes the adapted lineage pool while predation
   keeps removing its experienced members.

The 2×2 supports the interaction but does not prove the lineage mechanism.
The on/off-only cell keeps founder share at 13% with high population, so
founder presence alone is not sufficient. Direct lineage survival, death
cause, and trait distributions of the injected founders are the next
measurements.

## Caveats

- Four seeds were selected because they normally collapse; this is a
  controlled interaction test, not an unbiased frequency estimate.
- The off conditions are policy counterfactuals, not human play.
- 3,600 s and 20 seeds still do not measure the long-run probability of the
  high survivor.
- Predation share remains an upper bound when enrichment is active.
- The 20-seed survival run uses the normal planned policy; its high group is
  defined as final population >1,000.

## Reproduce

```bash
ROOT=/path/to/origo
SEED=20261301

EVOLUTION_DEMO_SMOKE_HOME=/tmp/controls-$SEED \
ORIGO_DEMO_SMOKE_SEED=$SEED \
ORIGO_DEMO_CAMPAIGN_STRATEGY=planned \
ORIGO_DEMO_CAMPAIGN_FRESH=1 \
ORIGO_DEMO_CAMPAIGN_PURCHASE_INTERVAL=10 \
ORIGO_DEMO_CAMPAIGN_ENRICHMENT=0 \
ORIGO_DEMO_CAMPAIGN_PREDATION=1 \
ORIGO_DEMO_CAMPAIGN_PURCHASE_LOG=/tmp/controls-$SEED.csv \
  bash "$ROOT/samples/Origo.EvolutionDemo/run-demo.sh" --no-build --headless \
  --seconds 2 --fast-forward 3600 --checkpoint-interval 300 --save-slot final
```

Use `ENRICHMENT=1` and/or `PREDATION=0` for the other cells. To reproduce the
lineage table, analyze 1,500 s, 1,800 s, and the final save together:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  genome \
  --save <saves>/save_iterate_1500 \
  --save <saves>/save_iterate_1800 \
  --save <saves>/save_final \
  --lineages-csv /tmp/lineages.csv \
  --metrics-json /tmp/metrics.json
```
