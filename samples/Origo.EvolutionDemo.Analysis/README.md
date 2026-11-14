# Evolution Demo Analysis Tools

Godot-free developer tools for the Evolution Demo. They are intentionally not
part of `Origo.sln` or CI; run them locally when analysing a save or a profile.

## Build

```bash
source scripts/bash/dotnet-env.sh
dotnet build samples/Origo.EvolutionDemo.Analysis/Origo.EvolutionDemo.Analysis.csproj -c Release
```

## Genome analysis

The tool reads the current v6 SND save produced by the demo. It accepts either
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
--k 0       automatic k via silhouette (default)
--k 4       fixed cluster count
--save ...  repeat for multiple saves/time points
```

Outputs:

```text
genome-report.html
genome-report.clusters.csv
```

The report contains:

- save/population/food overview;
- PCA scatter coloured by cluster;
- cluster feature profile;
- top differentiating structural/body/neural/behavioral features;
- raw feature distribution.

Feature groups include genome structure, organs, neural topology/weights,
energy budget, body traits, and save behavior fields (`drive`, displacement,
age, energy, children, kills, corpse meals, generation).

## Performance analysis

Enable the in-game profiler with environment variables:

```bash
ORIGO_DEMO_PROFILE_PATH="$PWD/.scratch/profile.jsonl" \
ORIGO_DEMO_PROFILE_INTERVAL=10 \
bash scripts/bash/run-evolution-demo.sh --headless --seconds 5 --fast-forward 600
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

The score report includes wall time, allocations, GC counts, population/food,
and per-system mean/P50/P95/P99/max from the pipeline phases:

```text
evolution.snapshot, evolution.corpse, evolution.core,
evolution.collision, evolution.foodbloom, evolution.commit,
evolution.foodsource, evolution.progression
```

## Limits

- Only current v6 saves are accepted; old saves are rejected explicitly.
- Behavior features are a single save snapshot; use multiple `--save` values for
  time-series comparisons.
- `neutral.*` features are brain outputs with zero sensor input, not full
  closed-loop behavior.
- Profiling is sample-based; set interval > 1 to reduce JSONL size.
