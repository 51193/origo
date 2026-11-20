# Evolution Demo Analysis Tools

Godot-free developer tools for the Evolution Demo. They are intentionally not
part of `Origo.sln` or CI; run them locally when analysing a save or a profile.

## Build

```bash
source scripts/bash/dotnet-env.sh
dotnet build samples/Origo.EvolutionDemo.Analysis/Origo.EvolutionDemo.Analysis.csproj -c Release
```

## Genome analysis

The tool reads the current v7 SND save produced by the demo. It accepts either
a save root, a directory containing `snd_scene.json`, or the JSON file itself.

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  genome \
  --save .godot-home-smoke/.local/share/godot/app_userdata/"Origo Evolution Demo"/evolution_demo_saves/save_auto \
  --out .scratch/genome-report.html \
  --max 5000 \
  --seed 1
```

`genome --help` prints the full option list. Options:

```text
--save <path>            v7 save root, scene directory, or snd_scene.json; repeat to pool
--out <path>             HTML report (default genome-report.html); a sibling .clusters.csv is always written
--max <n>                per-save analyzed row cap; 0 analyzes every creature (default 5000)
--seed <n>               seed for per-save sampling, k-means restarts, and bootstrap ARI (default 1)
--k <n>                  0 = automatic k via silhouette (default 0); N = fixed N clusters
--mode all|genotype|phenotype|behavior
--method kmeans|jaccard|dbscan
--stability <n>          true bootstrap ARI rounds, kmeans only (default 5; 0 = n/a)
--eps <x>                DBSCAN epsilon (0 = median k-distance estimate)
--min-points <n>         DBSCAN minimum points, counting the point itself (default 5)
--min-cluster-size <n>   auto-k validity floor (default max(2, ceil(0.01 * analyzed rows)))
--allow-skips            analyze the remaining creatures when a genome cannot be decoded
--timeline-csv <path>    one row per save: time, tick, counts, controller counters, feature means
--metrics-json <path>    machine-readable metrics, same numbers as the HTML report
--enrichment-csv <path>  every tested signature with counts, odds, p, and BH q values
```

Invalid or inapplicable options are errors: unknown methods/modes, `--k` with
DBSCAN, `--eps`/`--min-points` with k-means or Jaccard, `--stability` with a
non-k-means method, `--min-cluster-size` with DBSCAN, negative values, and
missing save inputs. Output directories are created as needed. Warnings go to
stderr and the HTML report.

### Outputs

```text
genome-report.html
genome-report.clusters.csv   name,save,lineage,generation,cluster (DBSCAN noise = -1)
timeline.csv                 only with --timeline-csv
metrics.json                 only with --metrics-json
enrichment.csv               only with --enrichment-csv
```

The optional timeline CSV has one row per save (sorted by simulation time) with
the save id, time, tick, full creature population, analyzed rows, food, corpses,
food sources, every numeric controller counter as `controller.<key>`, and the
mean of every extracted feature column. Prefer it over cluster composition for
time-series questions: a pooled clustering can separate snapshots by
age/complexity gradient instead of lineage.

The report contains:

- provenance header: command line, generation time, seed, `--max`, mode, method,
  requested/actual k, stability rounds, DBSCAN epsilon/min-points, minimum
  cluster size, analyzed/total population, and used/raw feature counts;
- save/population/food overview, ordered by simulation time and labeled by save
  id;
- PCA scatter with the explained-variance percentages on the axes, plus the
  per-component explained-variance table;
- cluster feature profile and top differentiating structural/body/neural/
  behavioral features;
- the full auto-k candidate table (k, inertia, silhouette, cluster sizes,
  accepted/rejected flags) and warnings for rejected candidates, a selected
  silhouette below 0.25, clusters below the validity floor, the search cap, and
  skipped creatures;
- cluster-by-save composition, a DBSCAN noise card, and noise excluded from the
  cluster tables and enrichment;
- Fisher exact gene-signature enrichment corrected with Benjamini-Hochberg over
  the full tested set (`m` shown, at most 10 rows per cluster displayed);
- raw feature distribution plus the names of dropped constant columns;
- a metric-definitions section stating every definition below.

### Metric semantics

- **Silhouette** is the standard Rousseeuw coefficient:
  `s(i) = (b(i) - a(i)) / max(a(i), b(i))`, where `a` is the mean distance to
  the other rows of the same cluster and `b` is the mean distance to the
  *nearest other cluster* over all of its rows (not the nearest other point).
  A row whose cluster is a singleton scores `s = 0`. Rows are scored on a
  documented deterministic systematic sample of at most 1000 rows (350 for
  Jaccard; index `floor(i * n / sampleSize)`), so the value is reproducible
  from the emitted labels. DBSCAN noise rows are excluded.
- **k-means** reports the minimum-inertia solution over 10 deterministic
  restarts; inertia is the sum of squared distances to the assigned centers.
  Auto-k evaluates every `k` in `2..min(8, n-1)` with the same restart budget
  (Jaccard k-medoids auto-k searches `2..min(6, n-1)` and has no inertia; its
  candidate table reports the silhouette and cluster sizes),
  computes the standard silhouette on the common sample, and selects the
  maximum-silhouette candidate whose clusters all reach the minimum cluster
  size `max(2, ceil(0.01 * n))` (configurable with `--min-cluster-size`). The
  floor is a validity constraint, not a metric change: silhouette rewards
  outlier singleton splits, so without it the default would select a
  one-creature cluster. When no candidate is valid the tool does **not** fall
  back to `k = 1`; it selects the maximum-silhouette candidate anyway, reports
  every rejection reason, and warns loudly. `k >= n` and `k < 0` are errors.
- **Bootstrap ARI** (k-means only) is the mean adjusted Rand index over
  bootstrap rounds: each round resamples `n` rows with replacement, fits
  k-means centers on the resample, assigns every original row to its nearest
  resampled center, and compares that labeling with the reference labeling.
  The report shows the mean and population standard deviation; non-k-means
  methods and `k = 1` report `n/a`, never `0.000`.
- **Gene-signature enrichment** applies a two-sided Fisher exact test per
  cluster versus the rest, then the Benjamini-Hochberg step-up over the *full*
  tested set (signatures present in at least 3 analyzed rows overall):
  `q(i) = min over j >= i of p(j) * m / j`, with `m` the tested count, so
  `q >= p` and `q <= 1`. The display limits rows; the correction never is.
  `--enrichment-csv` writes the complete tested set for FDR auditing.
- **DBSCAN** keeps noise label `-1`; the reported cluster count and silhouette
  exclude noise, the noise count is reported, and the core-point test counts
  the point itself (`min-points 3` = two other neighbours plus the point).
  `--eps 0` estimates epsilon from the median distance to the
  `min-points - 1`-th nearest other row (k-distance heuristic).
- **Organ and trait features.** `organ.*` is the number of expressed organ
  instances of every `OrganKindV4` (0 when the organ is absent). `trait.*` is
  the effective life-history value after the same fallback defaults the
  runtime phenotype adapter applies, for every `LifeTraitKindV4`; no feature is
  NaN by construction and a non-finite value is a hard error. Constant feature
  columns are dropped, named in the report, and the report shows used/raw
  counts.
- **PCA** components are orthonormal eigenvectors of the standardized
  covariance; the explained-variance ratio of a component is its eigenvalue
  share of the covariance trace, so the reported ratios sum to at most 1.
- **Sampling** is per save: `--max` samples rows from each save with a seed
  derived from `--seed` and the save order, so adding an unrelated save does not
  reshuffle another save's sample. The report states when the analyzed rows are
  a sample.
- **No silent data loss.** Creatures whose genome cannot be decoded are
  counted, named in the report, and fail the run unless `--allow-skips` is
  passed. The report and metrics JSON always show analyzed/total/skipped
  counts.

## Performance analysis

Enable the in-game profiler with environment variables:

```bash
ORIGO_DEMO_PROFILE_PATH="$PWD/.scratch/profile.jsonl" \
ORIGO_DEMO_PROFILE_INTERVAL=10 \
bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 5 --fast-forward 600
```

`ORIGO_DEMO_PROFILE_INTERVAL` is measured in simulation ticks (default `1`).
The profiler is disabled when `ORIGO_DEMO_PROFILE_PATH` is unset.

Generate the report:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  perf \
  --profile .scratch/profile.jsonl \
  --out .scratch/perf-report.html
```

Add `--from <simsec>` and/or `--to <simsec>` to aggregate only a simulation-time
window of the profile (useful for comparing early and late phases).

The report includes wall time, allocations, GC counts, population/food, and
per-system mean/P50/P95/P99/max, plus per-system allocation attribution and
subphases:

```text
evolution.snapshot, evolution.corpse, evolution.core,
evolution.collision, evolution.foodbloom, evolution.commit,
evolution.foodsource, evolution.progression

subphases:
evolution.core.ai, evolution.core.genome_decode,
evolution.core.sensors, evolution.core.brain,
evolution.core.spike_targets
```

`save` records also expose the full controller key/value set through
`SaveSnapshot.ControllerNumbers` / `ControllerStrings`, so callers can read
counters such as `evo.spikeKills`, meal totals, seed, and ticks without
re-parsing the save JSON.

Compare two profiles measured with the same scenario/seed:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release --   compare --baseline .scratch/profile-before.jsonl           --current .scratch/profile-after.jsonl           --out .scratch/perf-compare.html
```

Profile schema 2 is required; older profile JSONL is rejected explicitly. The
compare report covers wall/alloc/GC totals, every system (mean, p95, and mean
allocation), and every subphase (mean, p95, and mean allocation).

## Limits

- Only current v7 saves are accepted; old saves are rejected explicitly.
- Behavior features are a single save snapshot; use multiple `--save` values or
  `--timeline-csv` for time-series comparisons.
- A pooled clustering over multiple snapshots can encode time/age/complexity
  as cluster structure; use the auto-k candidate table and bootstrap ARI to
  judge stability and prefer per-save means when the question is temporal.
- `neutral.*` features are brain outputs with zero sensor input and are not
  clamped to the runtime action range.
- Saves store no parent pointer, so lineage composition is observable but a
  full phylogeny is not reconstructable from a save.
- Profiling is sample-based; set interval > 1 to reduce JSONL size.
