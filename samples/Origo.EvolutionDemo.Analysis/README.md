# Evolution Demo Analysis Tools

Godot-free developer tools for the Evolution Demo. The tool and its regression
suite are part of the root solution, so the normal `scripts/bash/format.sh` and
`scripts/bash/test.sh` gates cover them; they can still be run explicitly when
analysing a save or a profile.

## Build

```bash
source scripts/bash/dotnet-env.sh
dotnet build samples/Origo.EvolutionDemo.Analysis/Origo.EvolutionDemo.Analysis.csproj -c Release
```

## Genome analysis

The tool reads the current v7 SND save produced by the demo. It accepts either
a save slot directory, a `level_<id>` directory, or the `snd_scene.json` file
itself. A `--save` directory that resolves to several save scenes (for example
the parent directory of all slots) is a hard error unless `--pool` is passed;
`--pool` analyzes those scenes as one pooled input. The reader requires the
demo controller with `evo.genomeVersion = 7`, `evo.time`, and `evo.tick`;
missing metadata, malformed entities, and creatures without a usable
`evo.bodyGenome` are reported instead of being silently defaulted. Saves whose
slot lacks `.payload.sha`, or that contain a `.write_in_progress` marker, emit
integrity warnings.

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
--save <path>            v7 save slot, level directory, or snd_scene.json; repeat to pool
--pool                   allow one --save directory to expand to several save scenes
--out <path>             HTML report (default genome-report.html); a sibling .clusters.csv is always written
--max <n>                per-save analyzed row cap; 0 analyzes every creature (default 5000)
--seed <n>               seed for per-save sampling, k-means/Jaccard restarts, and bootstrap ARI (default 1)
--k <n>                  0 = automatic k via silhouette (default 0); N = fixed N clusters
--mode all|genotype|phenotype|behavior   behavior includes behavior/diet/access; origin is all-only
--method kmeans|jaccard|dbscan
--stability <n>          true bootstrap ARI rounds, kmeans only (default 5; 0 = n/a)
--eps <x>                DBSCAN epsilon (0 = median k-distance estimate)
--min-points <n>         DBSCAN minimum points, counting the point itself (default 5)
--min-cluster-size <n>   auto-k validity floor (default max(2, ceil(0.01 * analyzed rows)))
--allow-skips            analyze the remaining creatures when a genome cannot be decoded
--timeline-csv <path>    one row per save: time, tick, counts, ecology/access/diet deltas, controller counters, feature means
--timeline-percentiles   add p50/p90/p95 columns for key features to the timeline CSV
--features-csv <path>    one row per analyzed creature with every feature value and its cluster
--lineages-csv <path>    one row per living mutation lineage per save
--root-lineages-csv <path> one row per living root lineage per save
--root-mortality-csv <path> one row per root death total per save
--root-survival-csv <path> one row per root per save, living/extinct plus deltas
--metrics-json <path>    machine-readable metrics, same numbers as the HTML report
--enrichment-csv <path>  every tested signature with counts, odds, p, and BH q values
```

Invalid or inapplicable options are errors: unknown methods/modes, `--k` with
DBSCAN, `--eps`/`--min-points` with k-means or Jaccard, `--stability` with a
non-k-means method, `--min-cluster-size` with DBSCAN, negative values except
`--seed`, and missing save inputs. Output directories are created as needed.
Warnings go to
stderr and the HTML report.

### Outputs

```text
genome-report.html
genome-report.clusters.csv   name,save,lineage,generation,cluster (DBSCAN noise = -1)
timeline.csv                 only with --timeline-csv
features.csv                 only with --features-csv
lineages.csv                 only with --lineages-csv
root-lineages.csv            only with --root-lineages-csv
root-mortality.csv           only with --root-mortality-csv
root-survival.csv            only with --root-survival-csv
metrics.json                 only with --metrics-json
enrichment.csv               only with --enrichment-csv
```

The optional timeline CSV has one row per save (sorted by simulation time) with
the save id, time, tick, full creature population, analyzed rows, food, corpses,
food sources, the ecology/access/diet columns listed below, the mutation
`lineage.*` and stable `rootLineage.*` columns, spawn-origin counts, death-cause
counters, every controller counter as `controller.<key>` (numeric, string, and
boolean values), the mean of every extracted feature column, and (with
`--timeline-percentiles`) p50/p90/p95 for the key
age/energy/kill/generation/diet/body columns. A save that contributed
no analyzable rows keeps its row with a blank feature mean. Prefer it over cluster
composition for time-series questions: a pooled clustering can separate snapshots
by age/complexity gradient instead of lineage.

The ecology columns are computed from **every parsed creature and food entity**,
not from the optional `--max` clustering sample. In `--metrics-json`, the
per-save numeric `saves[].food` count remains unchanged; the typed inventory is
`saves[].foodInventory`:

- `food.soft/tough/toxic/corpse`: food inventory by type; corpse is a soft subset.
- `patch.count/patch.radius/patch.coverage`: live `world.foodPatchCount` (falling
  back to `world.config`), serialized patch radius, and `count*radius^2/pondRadius^2`.
  A mismatch between the live count and the serialized settings is reported as a
  warning and the live value is used.
- `origin.founderShare/meanGeneration/maxGeneration`: living generation-0 share,
  mean generation, and the controller's historical maximum generation.
- `access.foodInPickupShare/foodInSenseShare/foodIn300Share`: share of creatures
  with at least one positioned food inside the creature's saved pickup radius,
  saved sense range, or 300 world units. `access.meanFoodInSense` and
  `access.meanFoodIn300` are per-creature means; `access.nearestFoodP50` and
  `access.nearestSourceP50` are medians. Nearest food is capped at 600 units;
  nearest source at 10,000 units.
- `diet.living.*`: shares summed over all living creatures' meal counters.
  `diet.experienced.*`: the same over creatures with at least 10 meals.
  `diet.controller.*`: cumulative controller meal counters.
- `delta.population/births/kills/enrichmentSpawns`: change from the previous
  save. `delta.deathsEstimated = birthsDelta + enrichmentSpawnsDelta - populationDelta`
  (enrichment defaults to zero when the counter is absent). `delta.predationShareEstimated
  = killsDelta / deathsEstimated` when positive. These are estimates: a game build
  without `evo.enrichmentSpawns` makes the death estimate a lower bound and the
  predation share an upper bound.
- `lineage.count/founderLineages/founderLineageShare/gen0Share`: living
  mutation-lineage structure in the save. `largestShare` is the largest living lineage's share;
  `entropy` is normalized Shannon entropy over lineage sizes (0 with fewer than
  two lineages). `new` and `lost` compare lineage ids with the previous save;
  `newGen0` counts new ids that contain a generation-0 creature; `priorSurvival`
  is the share of previous ids still present; `turnover = (new + lost) /
  (previous + current)`. The first save has no prior comparison, so
  `priorSurvival` is blank and new/lost/turnover are 0.
  `newGen0` is a founder-injection proxy, not an enrichment-only marker: organ
  innovation also creates generation-0 messengers with new lineages.
  **Lineage ids are mutation clusters, not stable family ids.** The game assigns
  a new id when a child accumulates enough mutations, so `new`, `lost`,
  `priorSurvival`, and `turnover` mostly describe mutation-lineage churn.
  `rootLineage.*` uses `evo.rootLineage`, inherited unchanged through mutation
  lineage changes; it is the stable ancestry view. For saves written before the
  field existed, root lineage falls back to the mutation lineage.
- `origin.founders/children/enrichment/innovation/overgrowth/unknown`: living
  creatures by `evo.spawnOrigin`. Older saves report all `unknown`.
- `death.starvation/predation/aging/total`: cumulative controller counters for
  zero-energy death, lethal spike death, and maximum-age death. Older saves
  with none of the counters leave these columns blank; if only part of the
  counter set exists, the missing causes are treated as zero so partial data is
  still visible.

`--lineages-csv` writes one row per living mutation lineage per save:
`save,time,lineage,creatures,share,founderCreatures,minGeneration,maxGeneration,isNew,isNewGen0`.
`--root-lineages-csv` writes the same shape with `rootLineage` for stable ancestry.
`--root-mortality-csv` writes one row per root with cumulative starvation,
predation, aging, total, interval deltas, whether the root is still living in
that save, and its living creature count. `--root-survival-csv` joins living
root details and death counters: one row per root per save, with living,
extinct, cumulative causes, and interval deltas. The interval columns compare
consecutive saves; the first save's interval is its cumulative total.
`metrics-json` carries the same per-save values under `saves[].lineage`,
`saves[].rootLineage`, `saves[].spawnOrigins`, and `saves[].deaths`. Sequence
summaries are `lineageSequence` and `rootLineageSequence` (`distinctLineages`,
`peakLivingLineages`, `finalLivingLineages`, `totalNewLineages`,
`totalNewGen0Lineages`, `lostLineages`, `meanPriorLineageSurvival`,
`meanLineageTurnover`), `spawnOrigins`, and `mortalitySequence` (latest
cumulative counts and cause shares), `rootMortalitySequence` (latest
per-root totals and shares), and `rootMortality` (latest root × cause rows).

The report contains:

- provenance header: command line, generation time, seed, `--max`, mode, method,
  requested/actual k, stability rounds, DBSCAN epsilon/min-points, minimum
  cluster size, analyzed/total population, and used/raw feature counts;
- save/population/food overview, ordered by simulation time and labeled by save
  id, plus a table of the latest save's controller counters (numeric, string,
  and boolean values);
- a food/access/origin table with food-type inventory, live patch count/radius/
  coverage, food-in-sense and food-in-300 shares, nearest-food/source medians,
  founder share, mean generation, and living/experienced diet composition;
- living lineage tables for mutation lineages and stable root lineages, plus a
  spawn-origin table (founder, child, enrichment, innovation, overgrowth,
  unknown) and a cumulative death-cause table;
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
  **When the clustering mode is `genotype` or `all`, the gene/organ features
  also define the clusters**, so the enrichment is descriptive of the cluster
  definition rather than an independent adaptive test; the report warns about
  this. Use `phenotype` or `behavior` for an independent gene test.
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
- **Diet and niche features.** `behavior.softMeals`, `behavior.toughMeals`,
  `behavior.toxicMeals`, and `behavior.corpseMeals` are per-creature meal
  counters persisted by the demo. `diet.totalMeals` is soft+tough+toxic
  (corpse food is a soft-food subset and is reported separately), the
  `diet.*Share` features are shares of that total, `diet.dominance` is the
  largest food-type share, and `diet.entropy` is the normalized Shannon
  entropy over the three food types (0 for a single type, 1 for equal
  shares). `diet.hasMeals` is 1 when the creature has any meal, and
  `diet.corpseOfSoftShare` is the corpse share of its soft meals. Saves
  written before these counters existed read as zero rather than failing.
  **A zero-meal creature receives 0 for every diet share**, so means over all
  creatures are age-structure sensitive and do not form a composition; use
  `diet.living.*`, `diet.experienced.*`, or `diet.controller.*` for a
  population diet. The cluster behavior profile includes the per-type
  counters, shares, and entropy.
- **Food access, origin, and rate features.** `access.foodInPickup`,
  `access.foodInSense`, `access.foodIn300`, and `access.foodIn600` count
  positioned food inside the creature's pickup radius, saved sense range, or
  300/600 world units. `access.nearestFoodDistance` is capped at 600 units;
  `access.nearestSourceDistance` is capped at 10,000 units when no source
  exists. `origin.generationZero` and `origin.generationLe2` flag founder and
  recent-generation individuals. `behavior.killsPer100s`,
  `behavior.mealsPer100s`, and `behavior.childrenPer100s` divide lifetime
  counters by `max(age, 1)` seconds, so a young individual cannot look
  experienced. The `diet.*` and `access.*` features are included in the
  `behavior` mode; the `origin.*` cohort flags are included in `all` only.
- **PCA** components are orthonormal eigenvectors of the standardized
  covariance; the explained-variance ratio of a component is its eigenvalue
  share of the covariance trace, so the reported ratios sum to at most 1.
- **Sampling** is per save: `--max` samples rows from each save with a seed
  derived from `--seed` and the save's position in the canonical path-sorted
  input order. Reordering `--save` arguments does not change the sample; adding
  a save whose path sorts before an existing one can change which rows of later
  saves are sampled. The report states when the analyzed rows are a sample.
- **No silent data loss.** Creatures without a usable `evo.bodyGenome` are
  counted by the reader; creatures whose genome cannot be decoded or compiled
  are counted by extraction. Both fail the run unless `--allow-skips` is passed.
  The report and metrics JSON always show analyzed/total/skipped counts, and the
  report lists reader warnings (ignored malformed/unknown entities, integrity
  markers).

## Batch comparison

`batch` reads per-run metrics JSON files and compares regimes without
rerunning the simulation:

```bash
origo-analysis batch \
  --metrics out/seed-1/metrics.json \
  --metrics out/seed-2/metrics.json \
  --out batch.html --csv batch.csv --json batch.json
```

It reports per-run population, mutation/root lineages, enrichment founders,
deaths, mortality shares, roots with deaths, and top-root share/living state.
The HTML groups runs into high (>1,000 final creatures) and low regimes and
shows medians for population, roots, predation, and top-root share.

## Performance analysis

Enable the in-game profiler with environment variables:

```bash
ORIGO_DEMO_PROFILE_PATH="$PWD/.scratch/profile.jsonl" \
ORIGO_DEMO_PROFILE_INTERVAL=10 \
bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 5 --fast-forward 600
```

`ORIGO_DEMO_PROFILE_INTERVAL` is measured in simulation ticks (default `1`) and
the profiler is disabled when `ORIGO_DEMO_PROFILE_PATH` is unset. Only ticks that
are sampling boundaries are written, so each record describes one sampled tick;
the per-record `gc0/1/2` values are the collection counts of that single tick and
non-sampled ticks are not recorded at all.

Report a profile:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  perf \
  --profile .scratch/profile.jsonl \
  --out .scratch/perf-report.html
```

`perf --help` prints the full option list:

```text
--profile <path>         profile JSONL written by SimulationProfiler (schema 2)
--out <path>             HTML report (default perf-report.html)
--segment all|last|<n>   simulation segment selection, 0-based (default last)
--from <simsec>          inclusive lower simTime bound inside the selected samples
--to <simsec>            inclusive upper simTime bound inside the selected samples
```

### Simulation segments

The documented smoke-plus-benchmark run restarts the pond inside one process
while the profiler keeps appending, so one profile file can contain several
simulation segments. A new segment starts whenever the recorded `simTime`
decreases; all other samples belong to the current segment.

`perf` and `compare` default to the **last** segment because it is the
benchmark subject, print a warning on stderr, and state the selection in the
report. `--segment all` pools every segment (the historical behaviour),
`--segment last` makes the default explicit, and `--segment <n>` selects one
zero-based segment. `--from`/`--to` are then applied to the recorded `simTime`
of the selected samples (both bounds inclusive). With `--segment <n>` or the
default last segment the window cannot mix segments; `--segment all` pools the
segments first and a window may then span them. An empty window is an error.
`--from 0 --to 12` on a two-segment profile therefore selects 11 benchmark
samples, not 11 smoke plus 11 benchmark samples; add `--segment all` to pool
them.

### Systems and subphases

Phase ids are classified by the phase-marker parent: exactly one dot is a
top-level system; exactly two dots whose parent is a system is a subphase of
that system. Any other id, a missing parent, or a phase key set that is not
identical on every sample is a hard error instead of a silent zero-fill.

```text
systems:
evolution.snapshot, evolution.corpse, evolution.core,
evolution.collision, evolution.foodbloom, evolution.commit,
evolution.foodsource, evolution.progression

subphases of evolution.core:
evolution.core.ai, evolution.core.genome_decode,
evolution.core.sensors, evolution.core.brain,
evolution.core.spike_targets
```

Subphase time is measured inside its parent system, so subphases are listed
separately with their parent and are **never** added to the system share
denominator. The report states the denominator and the mean of wall time it is
approximately equal to. `share = system mean / sum of system means`.

### Statistics

- **Percentiles** use Hyndman–Fan type 7 linear interpolation. For a sorted
  sample `x[0..n-1]` and percentile `p`, the position is `h = (n-1) * p`,
  `lo = floor(h)`, and `P(p) = x[lo] + (h - lo) * (x[lo+1] - x[lo])`. P50 is
  therefore interpolated (the median), and P99 only equals the maximum for the
  degenerate small samples. Mean and maximum keep their usual definitions.
- **GC collection counts** are reported as a sum over the sampled ticks, labelled
  with the sample count (`GC 0/1/2 Σ(N samples)`). Because each record's counts
  cover a single tick, the sum scales with the sampling interval; the report also
  shows `est./run = sampled sum × tick interval`, an estimate of the run total
  that assumes uniform sampling. Both the sum and the estimate are approximate
  run quantities, not independent measurements. `compare` compares the
  normalized `est./run` values and states both intervals.
- **wall**, **alloc**, **population**, **food**, and **corpses** are per-sampled-tick
  statistics; the cards show P50 (median) values and the per-sample time-series
  chart is plotted within the selected segment and window.

The report header states profile path, sample count, simTime range, sampled tick
interval, segment count/selection, and window. It carries the system table with
mean/P50/P95/P99/max, share and mean allocation; a subphase table with parent;
population/food/corpses cards and chart; and warning notes.

`save` records also expose the full controller key/value set through
`SaveSnapshot.ControllerNumbers` / `ControllerStrings`, so callers can read
counters such as `evo.spikeKills`, meal totals, seed, and ticks without
re-parsing the save JSON.

### Compare

Compare two profiles measured with the same scenario/seed, sampling interval,
and duration (the report states whether those match):

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  compare \
  --baseline .scratch/profile-before.jsonl \
  --current .scratch/profile-after.jsonl \
  --out .scratch/perf-compare.html
```

`compare` accepts the same `--segment`/`--from`/`--to` options and applies them
to both profiles. Rows are the symmetric union of the two profiles: a metric
present in only one profile is rendered as `n/a` and listed in the warnings
instead of being silently dropped, and a zero baseline is `n/a` rather than a
meaningless `0.0 %` change. The table covers wall mean/P95/P99, allocation
mean/P95, interval-normalized GC `est./run` rows, and every system and subphase
(mean, P95, and mean allocation). The header carries sample count, simTime
range, sampled tick interval, segment selection, and GC sums/estimates for both
sides.

Profile schema 2 is required; older profile JSONL is rejected explicitly.

## Limits

- Only current v7 saves are accepted; old saves are rejected explicitly.
- The analysis reader does not verify the `.payload.sha` hash content; it warns
  when the hash file is absent or when a write-in-progress marker is present,
  and reads the scene JSON directly.
- Food without a finite `evo.x`/`evo.y` is counted in the inventory but excluded
  from spatial access metrics; the reader reports the count. Food without
  `food.type` reads as soft, and `food.fromCorpse` defaults to false, so older
  v7 saves remain readable but cannot retroactively distinguish types.
- `world.config` can be stale after a campaign pond expansion: the live
  controller `world.foodPatchCount` is used and a mismatch is warned about.
  `patch.coverage` is approximate (patch circles may overlap) and is `n/a`
  when settings are absent.
- The save-level access/diet/origin metrics use every parsed creature, while the
  cluster feature matrix uses the optional `--max` sample. A sampled run's
  `saves` table and `timeline` ecology columns therefore describe the full save,
  not the sampled rows.
- Behavior features are a single save snapshot; use multiple `--save` values or
  `--timeline-csv` for time-series comparisons.
- A pooled clustering over multiple snapshots can encode time/age/complexity
  as cluster structure; use the auto-k candidate table and bootstrap ARI to
  judge stability and prefer per-save means when the question is temporal.
- `neutral.*` features are brain outputs with zero sensor input and are not
  clamped to the runtime action range.
- Saves store no parent pointer, so lineage composition is observable but a
  full phylogeny is not reconstructable from a save. Gene signatures use stable
  gene ids plus payload values, so an enriched signature is lineage-local
  rather than a cross-lineage homologous trait.
- Profiling is sample-based. The schema has no header with the sampling
  interval, segment id, or phase kind, so the analyzer infers the interval from
  tick deltas, splits segments on a `simTime` reset, and classifies phases by
  id depth. The GC run total is therefore estimated, not measured.
