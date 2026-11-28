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

### Interval deltas

The first interval is run start to 1,500 s; the second and third are consecutive
checkpoints. These come from the `interval*` columns in `--root-mortality-csv`
and the joined `--root-survival-csv`.

| Cell | Root | 0–1,500 s | 1,500–3,000 s | 3,000–3,600 s | Living at final |
|---|---:|---:|---:|---:|---:|
| e1p0 | 4 | 8,590 (0 pred) | 20,437 (0 pred) | 4,662 (0 pred) | yes |
| e1p0 | 6 | 936 | 405 | 0 | no |
| e1p1 | 4 | 8,315 (1,758 pred) | 17,378 (8,557 pred) | 3,063 (2,253 pred) | yes |
| e1p1 | 6 | 2,387 | 4,265 | 0 | no |

Predation on root 4 accelerates sharply in the middle interval (8,557 deaths),
then remains the largest cause in the final interval (2,253 of 3,063). Root 6 is
already extinct by 3,000 s in e1p1 while root 4 survives.

At final, the survival CSV reports 407 living and 4,257 extinct roots in e1p0;
91 living and 3,923 extinct roots in e1p1. Root 4's first interval is the run
start to 1,500 s, so it is larger than a normal checkpoint delta.

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

## Five additional planned seeds

Five fresh seeds (20261411–20261415) with per-root death capture, 3,600 s:

| Seed | Final pop | Living roots | Enrichment founders | Predation | Roots with deaths | Extinct roots | Top-root share |
|---|---:|---:|---:|---:|---:|---:|---:|
| 20261411 | 505 | 117 | 71 | 51% | 4,201 | 4,114 | 35.0% |
| 20261412 | 342 | 96 | 52 | 50% | 4,029 | 3,952 | 37.6% |
| 20261413 | 278 | 86 | 46 | 44% | 3,481 | 3,412 | 38.3% |
| 20261414 | 411 | 89 | 54 | 49% | 4,077 | 4,008 | 32.8% |
| 20261415 | 237 | 80 | 42 | 36% | 2,302 | 2,231 | 60.4% |

All five were low-density, all five bought enrichment, and in every run the
largest root was **already extinct at final**. Unlike the seed-20261301 e1p1
run where root 4 absorbed 47% of deaths and survived, the top-root share here is
**32.8–60.4%** and the top root itself is gone. Root dominance is substantial
but the identity of the dominant root does not persist in these low runs; the
common result is a large pool of extinct roots, including the leading one.

## High vs low: per-root batch

The new `batch` command compares the per-root metrics:

| Regime | Runs | Median population | Median roots | Median predation | Median top-root share | Top root alive |
|---|---:|---:|---:|---:|---:|---:|
| high (>1,000) | 4 | 1,641 | 3 | 19.3% | 64.8% | 4/4 |
| low | 6 | 310 | 92.5 | 49.2% | 37.9% | 0/6 |

The high seeds (20261303, 20261317, 20261404, 20261405) had no enrichment,
1–5 living roots, and a top root that survived. All six low runs, including
20261311 (the one high-nominal seed that collapsed), had a top root that was
extinct at final. This is a sharper regime difference than the first five-seed
snapshot suggested.

## Unbiased 20-seed per-root batch

A fresh, unselected batch (20261501–20261520, 3,600 s):

| Regime | Runs | Median pop | Median roots | Median extinct roots | Median predation | Median top-root share | Top root alive |
|---|---:|---:|---:|---:|---:|---:|---:|
| high | 4/20 | 1,770 | 2.5 | 2 | 20.9% | 58.6% | 4/4 |
| low | 16/20 | 307.5 | 88 | 87 | 49.2% | 41.1% | 4/16 |

The high rate is **20%** with a 95% Wilson interval of **8.1–41.6%**; the
20-seed sample is still wide. High runs are the four seeds 20261501,
20261513, 20261518, and 20261519.

What survives the larger sample:

- high runs keep 2–3 living roots and 0–3 extinct roots;
- low runs keep 74–112 living roots but have a median 87 extinct roots;
- high predation is about 21% versus 49% in low runs;
- the top root survived in all 4 high runs but only 4 of 16 low runs.

Top-root share remains variable in low runs (18–74%). The earlier 10-seed batch
reported 1/10 high and a 4/9 low top-root survival rate; the larger batch raises
the high rate to 4/20 and lowers low top-root survival to 4/16, so both remain
small-sample-sensitive. The robust separation is **root number/extinction and
predation**, not top-root share alone.

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
