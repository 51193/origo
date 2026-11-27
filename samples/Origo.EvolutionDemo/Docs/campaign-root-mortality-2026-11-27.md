# Root × Death Cause — 2026-11-27

> [↑ Back to demo Docs](README.md)

## Problem

The 10-seed instrumented batch exposed aggregate mortality, but not which root
lineages absorbed the deaths. A root could be alive while most of its descendants
had died, or be extinct after dominating mortality.

## Decision

Add a cumulative per-root death map to the controller and expose it through
`--root-mortality-csv`, `rootMortality`, and `rootMortalitySequence`.

Run the same 2×2 control seed with this instrumentation:

- seed 20261301, fresh planned policy, 3,600 s;
- enrichment on in both cells;
- predation off (`e1p0`) and on (`e1p1`);
- 300 s checkpoints; analyze 1,500 / 3,000 / final;
- data: [campaign-root-mortality-2026-11-27.json](baselines/campaign-root-mortality-2026-11-27.json).

## Result

| Cell | Final pop | Living roots | Living root gen0 | Mutation lineages | Roots with deaths | Deaths | Predation share |
|---|---:|---:|---:|---:|---:|---:|---:|
| e1p0 (predation off) | 2,497 | 407 | 335 | 1,842 | 4,430 | 73,771 | 0% |
| e1p1 (predation on) | 425 | 91 | 48 | 321 | 3,991 | 60,832 | 55% |

### Dominant roots

| Cell | Root | Starvation | Predation | Aging | Total | Share of deaths |
|---|---:|---:|---:|---:|---:|---:|
| e1p0 | 4 | 30,706 | 0 | 2,983 | 33,689 | 45.7% |
| e1p0 | 26 | 5,585 | 0 | 228 | 5,813 | 7.9% |
| e1p1 | 4 | 15,351 | 12,568 | 837 | 28,756 | 47.3% |
| e1p1 | 6 | 3,696 | 2,321 | 635 | 6,652 | 10.9% |

The aggregate counts match the root × cause sums exactly in both cells:
73,771 and 60,832 tracked deaths.

### What this adds

- The same dominant root (`4`) absorbs roughly **46–47%** of all tracked deaths
  in both cells. Predation does not spread evenly across roots.
- Enrichment creates thousands of short-lived roots: e1p0 has 407 living roots
  but **4,430 roots with deaths**; e1p1 has 91 living roots and 3,991 roots with
  deaths.
- In e1p1, root 4 loses 12,568 creatures to predation and 15,351 to starvation;
  the root survives but is small at final. Predation is **not** merely removing
  rare founders; it removes a large fraction of the dominant root's output.
- The root × cause view supports the founder-pressure loop from the 2×2:
  enrichment supplies roots, predation removes their descendants, and a small
  set of roots dominates the surviving population.

## Caveats

- Root ids are stable ancestry, but not a complete parent tree.
- The death map is cumulative; interval deaths require differencing consecutive
  saves.
- A root can have zero deaths and still be absent from the per-root death table.
- These are two cells of one seed; the 10-seed planned batch remains the
  frequency evidence.

## Reproduce

```bash
# same run script as campaign-controls-2026-11-26, enrichment=1
# e1p0: ORIGO_DEMO_CAMPAIGN_PREDATION=0
# e1p1: ORIGO_DEMO_CAMPAIGN_PREDATION=1

dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  genome --save <saves>/save_iterate_1500 \
  --save <saves>/save_iterate_3000 --save <saves>/save_final \
  --root-lineages-csv /tmp/roots.csv \
  --root-mortality-csv /tmp/root-deaths.csv \
  --metrics-json /tmp/root-mortality.json
```
