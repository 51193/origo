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

Optional:

```text
--k 0          automatic k via silhouette (default)
--k 4          pin exactly 4 clusters (no auto search)
--mode all|genotype|phenotype|behavior
--method kmeans|jaccard|dbscan
--stability 5  bootstrap ARI rounds (kmeans only)
--eps 0        DBSCAN epsilon (0 = median k-distance)
--min-points 5 DBSCAN minimum points
--save ...     repeat for multiple saves/time points
--timeline-csv timeline.csv
               per-save feature means written for time-series analysis
```

Outputs:

```text
genome-report.html
genome-report.clusters.csv
timeline.csv        (only with --timeline-csv)
```

The optional timeline CSV has one row per save (sorted by simulation time) with
the save time, population, food, corpses, source count, and the mean of every
extracted feature column. Prefer it over cluster composition for time-series
questions: a pooled clustering can separate snapshots by age/complexity
gradient instead of lineage.

The report contains:

- save/population/food overview;
- PCA scatter coloured by cluster;
- cluster feature profile;
- top differentiating structural/body/neural/behavioral features;
- raw feature distribution;
- cluster-by-save composition, ordered by simulation time;
- Fisher exact gene-signature enrichment with BH FDR;
- bootstrap ARI and (for genotype) readability labels such as
  `Organ(SpikeOrgan,size=...)`, `Reflex(FoodAhead->Move,gain=...)`.

Feature groups include genome structure, organs, neural topology/weights,
energy budget, body traits, and save behavior fields (`drive`, displacement,
age, energy, children, kills, corpse meals, generation).

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
  as cluster structure; treat silhouette and bootstrap ARI as the stability
  check and prefer per-save means when the question is temporal.
- `neutral.*` features are brain outputs with zero sensor input, not full
  closed-loop behavior.
- Profiling is sample-based; set interval > 1 to reduce JSONL size.
