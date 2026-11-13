# Origo Evolution Demo

A playable idle artificial-life sandbox built on `Origo.Core` and
`Origo.GodotAdapter`.

## What you do

- Start a pond from the main menu or load a saved lineage.
- Watch 900 founders forage, move, reproduce, mutate, and die in a huge
  circular pond (10,000 world-units in radius; up to 2,500 creatures; the cap is
  a safety valve, not a normal operating limit). Creatures are deliberately small
  at this scale, so the default local view reads as a sparse world.
- The default camera shows a local region; wheel zoom and right-drag pan let you
  explore the space, and `R` resets to the whole pond.
- Click a creature to inspect its V4 genome graph, phenotype,
  sensor readings, and outputs.
- Pause, change speed, drop nutrients, save, load, delete saves, or seed a new
  pond.
- Open **Diversity** in the HUD for live body-genome statistics, theme/lineage
  distributions, pattern usage, and a diversity history chart.
- Use **Iterate** to run forward to an absolute simulation time at maximum CPU
  speed while showing a progress bar plus global/instantaneous speed
  multipliers; it runs the same simulation ticks and deferred spawn/kill work as
  the normal pond.
- The HUD `Spikes` counter is the number of successful directional spike hits;
  directional hits are lethal, so the parenthesized `K` value tracks kills.
- Speed 1x/2x/4x/8x; auto-save every 60 seconds while running.

There is no direct creature control and no hard fail state: the pond is an
experiment. Metabolism, movement, and reproduction are deliberately costly, so
creatures that only spin in place eventually starve; successful lineages must
find food. If every creature dies, the Extinction panel offers a new pond or an
earlier save.

## Running

The repository script builds and launches the demo, or runs its headless
acceptance smoke:

```bash
# Build and launch the windowed demo.
bash scripts/bash/run-evolution-demo.sh

# Skip the build and launch the already-built demo.
bash scripts/bash/run-evolution-demo.sh --no-build

# Headless smoke with a chosen simulated target.
bash scripts/bash/run-evolution-demo.sh --headless --seconds 120

# 1h genome screening benchmark in the isolated smoke save home.
bash scripts/bash/run-evolution-demo.sh --headless --seconds 5 \
  --fast-forward 3600 --save-slot bench_1h
```

The windowed runner stores Godot user data in `.godot-home/`; headless smoke
uses a separate `.godot-home-smoke/`, so test runs can never overwrite player
saves. Override them with `EVOLUTION_DEMO_HOME` and
`EVOLUTION_DEMO_SMOKE_HOME` respectively. The runner prints the active user-data
directory on startup. Windowed saves live under:
`.godot-home/.local/share/godot/app_userdata/Origo Evolution Demo/evolution_demo_saves/`.

## Controls

| Input | Action |
|-------|--------|
| Main menu | Continue / New Game / Load Game / Quit |
| New Game | Seed, mutation preset, food preset, collision off/soft, early bloom off/on |
| Click creature | Open body/brain/stats inspector |
| Pause | Open pause menu; Resume returns to the pond |
| Feed | Drop a nutrient cluster; 5-second cooldown |
| Save | Open save slots (3 manual + auto) |
| Menu | Open pause menu |
| Escape | Close an open panel or toggle pause |
| Mouse wheel | Zoom in/out around the cursor |
| Right/middle drag | Pan the world view |
| `R` | Reset the camera to fit the whole pond |

## Advanced settings

The New Pond screen has an **Advanced Settings…** page (scrollable) that edits a
central `DemoSettings` model. Numeric settings use both a slider and a typed input
field; applying them starts a new pond with that configuration, and the settings
JSON is stored on the controller entity so save/load preserves them.

The page covers:

- world radius, initial population, hard cap, body/camera-scale physics;
- food amounts, food caps, feeding, bloom parameters;
- body gene ranges for size, speed, turn, sense, spike, metabolism, age, body
  radius, pickup padding, and max-energy scaling;
- spike salvo base/per-pellet/distance cost and cooldown per extra pellet;
- brain mutation, body-length mutation, frameshift, founder mutation,
  metabolism, reproduction, ageing, lineage threshold;
- spike threshold, base cost, cost per world-unit, aim half-angle, beam radius,
  drain fraction, range, and cooldown;
- carcass food interval, feeding amount/cooldown, and related world settings;
- tough/toxic food shares and their energy values;
- every food-patch/source parameter: count, radius, falloff, local capacity,
  scan/spawn/walk cadence, walk speed/turn, and all long-cycle bounds;
- global overgrowth scan interval, cell size, food threshold, mutation rate,
  consumed-food cost, and child energy.

Seed, food preset, normal mutation preset, collision, and bloom remain on the
main New Pond page. `Tuning` is now a compatibility facade over
`DemoSettings.Current`; architecture and rendering read the same active settings.

## Timed iteration

The HUD **Iterate** button opens an absolute-target dialog: enter a simulation
time, not an extra duration. For example, entering `3600` stops at sim time
3600.0, regardless of the current time. During iteration the pond and HUD are
hidden behind a single progress bar showing:

- current / target simulation time;
- **global multiplier** — simulated seconds per real second since iteration started;
- **instantaneous multiplier** — the same ratio over the latest sample window.

The controller advances one normal simulation tick per rendered frame and lets
the adapter flush the same deferred spawn/kill work as the playable loop.
`Engine.MaxFps` is uncapped and VSync is disabled while iterating, then restored.
Escape cancels. The normal simulation path and timed iteration share
`SimulateTick`; there is no alternate headless model.

The HUD **Real** clock is total wall-clock play time for the pond and is persisted
on the controller (`evo.realSeconds`). Loading a save resumes that total instead
of resetting to zero; save and autosave write it before requesting persistence.
Food sources have no center icon (it read as a creature at normal scale). Their
production radius is shown only as a faint translucent wash; the wash is skipped
once its screen-space radius exceeds 75% of the viewport height, so high zoom does
not tint the entire view or draw a screen-filling arc.

## Idle unlocks and content packs

Gameplay content is registered through `GameplayContentRegistry` and applied
through `GameplayRuntime`. Demo code obtains the runtime from the context-scoped
`EvolutionGameSession`; the runtime is no longer resolved through a process-global singleton. The runtime owns the current immutable content
snapshot, progression state, unlock service, and mechanic resolver. The fixed tick is driven by
`GameplayPipeline` with distinct systems per phase:

- `SnapshotSimulationSystem` (Sense) builds the entity/sensor snapshot;
- `CorpseSimulationSystem` (Sense) releases carcass nutrients;
- `CoreSimulationSystem` (Act) runs creature ecology;
- `CollisionSimulationSystem` (Move) resolves soft creature collisions;
- `FoodBloomSimulationSystem` (ResolveAction) adds feed and uniform food;
- `CommitSimulationSystem` (Commit) writes counters, deferred spawns, and
  extinction state;
- `FoodSourceSimulationSystem` (Environment) ticks food sources;
- `ProgressionSimulationSystem` (Progress) advances lifetime and milestones.

`DemoUi` is split into partial view files (`DemoUi.views.cs`,
`DemoUi.panels.cs`, `DemoUi.settings.cs`, `DemoUi.saveload.cs`,
`DemoUi.slots.cs`) around gameplay state, panels, settings, save/load, and slot
lists. `CoreEvolutionModule` implements `IGameplayModule` and registers the default
content plus all phase systems through the same `GameplayModuleServices`
contract. The demo no longer has a second hand-written system registration path;
additional modules can add content, handlers, and systems together. Death and spawn
policy behavior is dispatched through typed handler registries
(`GameplayHandlerRegistry<TContext>`) instead of handler-id switches in simulation. Milestone definitions are evaluated
against lifetime, population, and kill metrics each simulation tick and may
award points and unlock ids. Content is always registered before it is gated, so locked and
unlocked content share the same validation path.

The controller mirrors progression as `game.points`, `game.unlocked`,
`game.milestones`, and `game.lifetimeSeconds` for save snapshots. A separate
`user://gameplay_profile.json` is the authoritative meta-progression store: it
survives New Game and application restarts by design, because idle unlocks are
account-wide. Profile writes use a temp file plus atomic move; unreadable
profiles are warned and skipped, corrupt JSON is quarantined with a warning, and
gameplay falls back to placeholder defaults. Save version 6 rejects older saves. Evolution Points currently start at a very large placeholder value; the
unlock shop is functional, but economy balancing is intentionally deferred.

Press `U` while a pond is running to open the generic unlock shop. Rows are
generated from `UnlockDefinition` data, not from a hardcoded organ list. Buying
an unlock writes the progression back to the controller immediately.

JSON content packs live under `Content/` and are polled by
`DemoContentWatcher`. A valid pack is merged into the built-in content at a
frame boundary; invalid packs leave the running snapshot untouched. Content
reload only; C# handlers, systems, and SND strategies remain startup-loaded.

Mutually exclusive mechanics use mechanic-group variants. The death handler
selects exactly one variant for a death event; the default corpse-pulse variant
preserves the existing behavior. `unlock.spawn.enrichment` enables the
enrichment spawn variant, which can create a new founder at a food source. Gene,
organ, action, and sensor availability is filtered through the same unlock gate
before founders or mutations are generated.

## Genome: Genome v4 graph

Each creature carries one canonical V4 genome. The genome is a list of
stable-ID genes, not a fixed codon table:

- **Gene** — identity, kind, copy count, expression, typed payload, and
  regulatory conditions.
- **BodyPlan** — arbitrary segment and organ instances. Organ copy count is a
  gene property; there is no fixed organ-slot array.
- **NeuralGraph** — sensor, interneuron, and output nodes plus synapse genes.
  There is no fixed 372-hex latent lattice, no fixed 12-sensor array, and no fixed
  hidden-layer shape.
- **Regulation** — genes can require low food, nearby predators, or low energy.
- **Interface registry** — sensor/action definitions have stable IDs and labels;
  new interfaces are registered rather than allocated as fixed genome slots.
  `CarrionSense` is the first post-migration sensor gene: it exposes a corpse
  direction to the neural graph and is displayed by the inspector.
- **Registry-driven sensor runtime** — compiled phenotypes expose only their
  expressed SensorPorts/ActionPorts. The demo evaluates those ports through the
  sensor registry instead of a fixed 12-slot switch. Action dispatch is the next
  seam to generalize.
- **Energy budget** — body tissue, neural tissue, and genome complexity all cost
  maintenance. Cheap reflex creatures and expensive large-brain creatures can
  coexist.

The controller stores `evo.genomeVersion = 6`. Older saves are intentionally
not migrated; the Load panel rejects them with an explicit message. Genome
serialization is a canonical binary codec with a hex representation for SND
entities and debugging.

Mutations operate on stable gene identities: expression, copy count,
duplication/deletion, payload parameters, neurons, synapses, organs, and
regulatory links. A mutation is accepted only if the resulting genome validates
and compiles, so random search stays in viable phenotype space.

The detailed architecture and mock benchmark are in
[`Origo.EvolutionDemo.Logic/V4/README.md`](../Origo.EvolutionDemo.Logic/V4/README.md).

### Food types and digestive niches

Food sources can produce three food categories: soft, tough, and toxic. All
bodies can eat every category, but tough food yields full energy only with the
`HardDigest` organ and toxic food yields full energy only with the
`ToxinResistance` organ. Without the matching organ, the food is still edible at
reduced value, so a new food type cannot starve a population before digestion
mutations appear. Founder bodies start with a mix of battery, hard-digest, and
toxin-resistance organs.

The controller records `evo.foodSoftMeals`, `evo.foodToughMeals`, and
`evo.foodToxicMeals`, making niche divergence measurable from a save.

### Death and carcass nutrients

A dead creature becomes a `corpse` entity instead of immediately spawning all
death food. The corpse persists and releases its `DeathFoodCount` nutrients in
pulses at `CarcassFoodIntervalSeconds`; released food is normal pond food. If
the global food cap is full, the corpse waits and preserves its timer until
capacity frees up. This turns kill sites into temporary feeding hotspots and
gives scavenging lineages a reason to follow combat. Corpses also appear as
food-like attractors to the existing food sensors, so creatures steer toward
them without a new neural sensor slot. Food released by a corpse is tagged, and
the controller counts actual corpse meals (`evo.corpseMeals`) as evidence of
scavenging.

### Directional spike

The spike is no longer a radius AoE. The body's `SpikeOrgan` is the organ gene
and the body `SpikeRange` codon group is the maximum shot distance. A
`SpikeBattery` organ plus `SpikeCount` / `SpikeSpread` / `SpikeJitter` codons
turn the action into a salvo of rays; half of calibrated founders carry the
battery. The fixed latent brain's fourth output slot (`Aim`) supplies a signed
aim offset:

```text
aimAngle = bodyHeading + aimDrive * SpikeAimHalfAngle
```

Each pellet tests creatures inside a ray/beam, picks the nearest valid target
not already hit by this salvo, kills it, steals 70% of its current energy into
the shooter, and spawns the normal death-food amount at the target position.
The defaults make predation a routine part of the pond rather than a rare
event: a low `0.05` drive threshold, a wide 32-unit beam, a 1.0 s cooldown, and
a cheap base cost. The fan count, angle, and per-pellet jitter come from body
genes. Energy cost is:

```text
SpikeBaseCost + SpikePerPelletCost * pelletCount
+ SpikeCostPerUnit * shotDistance
```

A miss is charged at maximum range. The view draws the red beam from shooter to
impact endpoint. This turns spike into a directional, distance-priced predation
trait instead of an area clear.

Every kill makes the shooter a veteran: its rendered body grows by up to 75%
and its body color blends toward gold as it accumulates kills and scavenged
corpse meals. The growth is cosmetic and is not inherited by offspring.

## Appearance

Each creature's body color, accent color, pattern family, theme name, and
pattern parameters are derived directly from its raw genome string. The
derivation uses normalized position scans and smooth weights, not fixed
offsets, so it automatically adapts when the genome length changes: similar
genomes stay visually related while mutations can shift hue, pattern, and
rhythm details.

- Body and accent RGB are folded directly from two raw-genome segments: six
  hexadecimal nibbles become three 0–255 channel bytes. There is no HSL or
  theme-palette remap for the body; similar genomes keep similar byte colors.
- A thin screen-constant white rim is drawn at the judgment/body edge, with a
  minimum-width ring at extreme zoom-out, so bodies stay legible against food,
  source washes, and similarly colored neighbors.
- Twelve body pattern families are available: plain, ring, double ring,
  stripes, spots, rays, spiral, chevron, segments, cross, halo, and tri-dots.
- Eight named hue themes are used by the dashboard/legend: Ember, Amber, Moss,
  Reef, Tide, Dusk, Bloom, and Frost.
- Pattern angle, scale, and frequency are also genome-derived; no per-creature
  random seed is stored outside the genome.

Veteran growth is displayed on top of the genome-derived look: bodies grow by up
to 75% and tint toward gold as the creature gathers spike kills and scavenged
corpse meals, so a successful predator is visibly larger and warmer than its
offspring. The growth is cosmetic, not inherited, and is reported in the
inspector as Kills, Scavenged, and Growth.

The inspector shows the theme, pattern, and body hex color for the selected
creature. The Diversity board derives its metrics from genomes through the same
appearance logic.

## Simulation

The SND level `pond` contains one `world_controller` entity carrying both
`evolution.world` and `evolution.overgrowth`. The controller advances a fixed
10 Hz simulation, reads all creature/food entity data, and schedules reproduction,
food spawning, and death through Origo's deferred queue. Creatures and food are
SND entities with empty Godot placeholder nodes; `EvolutionWorldView` renders the
pond directly from their TypedData, which keeps hundreds of creatures cheap to
animate.

Food is deliberately scarce overall, but it is generated as **clustered
settlements** rather than uniform noise: 16 deterministic patch centers receive
initial and manually-fed food, and each patch is driven by its own SND food-source
entity. Within each patch the radial density is graded as
`(1 - r/R)^falloff` (`falloff = 0.55` by default for the 900-radius patch):
food is densest at the core and smoothly thins toward the rim, with a sparse
halo instead of a hard uniform-disk
edge. Patch centers are derived from the pond seed, so save/load preserves the
layout. The food target is about 3,000 over the 10,000-radius pond; movement and
metabolism are expensive enough that an orbiting creature that never finds a
patch will starve, and the 2,500-creature cap remains a safety valve.

### Adaptive SND food sources

Each patch owns a `food_source_<n>` entity with the `evolution.foodsource`
strategy. Once per second it scans food near its current position; every half
second it attempts a small spawn burst. Scan, spawn, and walk clocks start at a
deterministic per-source phase offset derived from the pond seed and patch
index, so the sixteen sources never scan or spawn on the same simulation tick.
The scan counts patch food through the tick's spatial grid and reads the pond's
global food total from the same tick snapshot, so a source never walks the whole
session; existing saves keep the clocks they persisted. The spawn chance is
`(1 - localFood/localCapacity) * cycleFactor`, so food-rich patches stop producing
while empty patches recover. Each source has a local capacity of 240 food,
keeping unattended patches from consuming the global hard cap and starving
occupied ones.

Sources also **randomly walk** through the pond in 0.5-second steps at
4–16 world-units/second, with a random heading and boundary steering. Their food
washes therefore migrate rather than staying at fixed coordinates. Source state
(position, heading, walk speed/step, scan/spawn clocks, local counts, next food
id) is stored on the entity, so save/load preserves the feedback loop.

Every source has an independent long-period production wave:

- period **90–240 s**;
- initial phase **0–period**;
- trough multiplier **0.05–0.35**;
- peak multiplier **1.4–2.5**.

The wave multiplies the local spawn chance, creating staggered resource booms
and droughts. Combined with migration and predation, this raises selection
pressure without changing the per-tick simulation semantics.

Two new-pond switches are deliberately exposed and both default to off:

- **Collision**: soft circular separation can be enabled per pond; the sparse
  default turns it off so creatures can pass through one another.
- **Early food bloom**: the three-minute food ramp is retained in code but off by
  default; enable it for a more forgiving cold start.
- Food patch count, radius, radial falloff, local capacity, and source
  scan/spawn cadence are central tuning values (`Tuning.FoodPatchCount`,
  `Tuning.FoodPatchRadius`, `Tuning.FoodPatchFalloff`,
  `Tuning.FoodPatchLocalCapacity`, `Tuning.FoodSourceScanIntervalSeconds`,
  `Tuning.FoodSourceSpawnIntervalSeconds`, `Tuning.FoodSourceSpawnBurst`).

### Global overgrowth

The controller also carries an `evolution.overgrowth` lifecycle strategy. It has
no presentation node of its own. Every configured interval it bins food into
density cells, finds the highest-density cell above the configured threshold, then:

1. picks a random living creature as the parent;
2. applies a large mutation (`OvergrowthMutationRate`, default 25% per digit);
3. consumes a large batch of food from that overgrown cell;
4. spawns the mutated descendant into the same high-density region.

The `OG` HUD counter records how many overgrowth descendants were created. This
is deliberately more expensive than normal reproduction and prevents a moving
food patch from remaining an uncontested "pig farm".

The large-population simulation uses three scale-oriented optimizations:

- a per-tick spatial grid for food/creature sensing and pickup, replacing the
  previous all-pairs sensor scan;
- adaptive AI batching: movement and energy still update every 10 Hz tick, while
  a bounded batch of about 600 creatures recomputes sensors/brains per tick at
  high populations (each creature rotates through the batch);
- X-sweep collision for large crowds and a render LOD that drops per-creature
  patterns/direction/energy details above ~900 visible creatures.

Creatures are rendered at their true world size (`BodyRadius * camera zoom`);
there is no minimum screen-size expansion, so at low zoom they become tiny dots
instead of overlapping into blobs. The camera fits the whole pond on `R`, but
starts zoomed into a local region; wheel zoom is cursor-relative and right/middle
drag pans with focus clamping. Zoom in to inspect bodies and patterns. The HUD
exposes the 2,500 hard cap so it is visible when the safety valve is approached.

Movement uses render interpolation: every tick stores previous/current position
and heading, and the view lerps between them using the controller's simulation
accumulator. The simulation can stay at 10 Hz for evolution/perf while 1x/2x
motion remains smooth. The HUD shows both accelerated `Sim` time and wall-clock
`Real` time so the x8 speed is explicit.

Save/load is handled entirely by Origo. `DemoEntry.ConfigureSaveMetadataContributors`
adds population, generation, lineage count, mutation rate, seed, pond time, and
timestamp to each save's display metadata. `DemoUi` uses the public
`ISndSaveOperations` API; no save files are written by demo code directly.

## Project layout

```
samples/Origo.EvolutionDemo/
  Main.tscn                     # one root Node: DemoEntry
  Origo/entry/entry.json        # custom level id "pond"
  Origo/initial/levels/pond/    # controller entity scene
  Origo/demo/*.tscn             # controller/creature/food/food-source placeholder nodes
  GENOME_V4_DESIGN.md          # Genome v4 architecture baseline
  Content/                      # hot-reloadable gameplay content packs
  Scripts/
    DemoEntry.cs                # OrigoDefaultEntry, paths, save metadata
    DemoContentWatcher.cs       # content pack polling + atomic apply
    DemoUi.cs                   # main menu, HUD, inspector, pause/save/load
    UnlockOverlay.cs            # generic U-key unlock shop
    GameplayStateSync.cs        # progression persistence on the controller
    DemoSmokeRunner.cs          # optional headless acceptance runner
    EvolutionWorldView.cs       # pond/creature/food drawing
    EvolutionEntityFactory.cs   # shared creature/food spawn metadata
  Strategies/
    WorldSimulationStrategy.cs  # simulation, mutation, controller state
    FoodSourceStrategy.cs       # adaptive per-patch food production
    OvergrowthStrategy.cs       # global high-density mutation pressure
```

`Origo.EvolutionDemo.Logic` contains the Godot-free V4 genome, phenotype
adapters, sensor, appearance, food-patch, collision, diversity, and central
`DemoSettings` layer. Its tests are in `Origo.EvolutionDemo.Logic.Tests`.

## Known first-version limits

- One pond; predation, scavenging, and food niches are present, but there are no
  scripted goals or direct creature control.
- Point/step mutations and body-length mutations only; no crossover.
- Neural topology is gene-expressed within safety and energy caps; no fixed
  latent lattice.
- Soft collision uses each creature's body radius; it remains off by default.
- No offline progression; the simulation runs while the game is open.
- Procedural vector visuals only; no external art/audio assets.
- UI text is English.
