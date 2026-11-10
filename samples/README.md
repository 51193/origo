# Samples

Playable consumer-facing examples built on the Origo shell packages.

## Origo Evolution Demo

A playable artificial-life idle sandbox: watch creatures with small neural
genomes forage, reproduce, mutate, and split into lineages. See the
[demo README](Origo.EvolutionDemo/README.md) for controls, the genome layout,
and the save/load behavior.

### Requirements

- .NET SDK pinned by [`global.json`](../global.json); run
  `bash scripts/bash/install-dotnet.sh` if it is not installed.
- Godot 4.7.2 (the same version pinned by `Origo.GodotAdapter.csproj`):
  `bash scripts/bash/download-godot.sh` downloads and caches the matching binary.

### Run

```bash
bash scripts/bash/download-godot.sh
./dotnet build samples/Origo.EvolutionDemo/Origo.EvolutionDemo.csproj
GODOT="$(bash scripts/bash/download-godot.sh)"
"$GODOT" --path samples/Origo.EvolutionDemo
```

The demo uses `ProjectReference` entries to the local framework projects, so it
runs directly from a development checkout. The NuGet package-adoption contract
is covered separately by `tools/ShellPackageConsumer` and
`scripts/bash/package-consumer-smoke.sh`.

Saves are written under `user://evolution_demo_saves` (on Linux typically
`~/.local/share/godot/app_userdata/Origo Evolution Demo/evolution_demo_saves`).
Delete that directory to reset all saves and the system blackboard.

### Headless smoke

The demo has a deterministic local acceptance mode for checking startup,
simulation, save, and load without opening a window:

```bash
GODOT="$(bash scripts/bash/download-godot.sh)"
ORIGO_DEMO_SMOKE=1 ORIGO_DEMO_SMOKE_SECONDS=30 \
  "$GODOT" --headless --path samples/Origo.EvolutionDemo --quit-after 4000
```

It prints `DEMO_SMOKE_OK ...` on success and exits non-zero on a failed check.
This is a developer verification path, not a framework gate. Setting
`ORIGO_DEMO_SCREENSHOT_DIR` additionally writes `menu.png`/`game.png` captures
for local UI review.

### Genome v4 prototype and benchmark

The clean Genome v4 architecture, tests, and mock benchmark live in
[`Origo.EvolutionDemo.Logic/V4`](Origo.EvolutionDemo.Logic/V4/README.md) and
[`Origo.EvolutionDemo.GenomeV4.Bench`](Origo.EvolutionDemo.GenomeV4.Bench/).
The benchmark is intentionally independent of the playable runtime until the
migration is complete.

### Logic tests

```bash
./dotnet test samples/Origo.EvolutionDemo.Logic.Tests/Origo.EvolutionDemo.Logic.Tests.csproj
```

The pure genome/neural-network/sensor layer has no Godot dependency and is part
of the normal solution test run.
