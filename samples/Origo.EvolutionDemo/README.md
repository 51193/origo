# Origo Evolution Demo

A playable idle artificial-life sandbox built on `Origo.Core` and
`Origo.GodotAdapter`.

## What you do

- Start a pond from the main menu or load a saved lineage.
- Watch 1,200 founders forage, move, reproduce, mutate, and die in a huge
  circular pond (10,000 world-units in radius; up to 3,000 creatures; the cap is
  a safety valve, not a normal operating limit). Creatures are deliberately small
  at this scale, so the default local view reads as a sparse world.
- The default camera shows a local region; wheel zoom and right-drag pan let you
  explore the space, and `R` resets to the whole pond.
- Click a creature to inspect its 103-hex-digit genome, sensor readings, and
  outputs.
- Pause, change speed, drop nutrients, save, load, delete saves, or seed a new
  pond.
- Open **Diversity** in the HUD for live genome statistics, theme/lineage
  distributions, pattern usage, and a diversity history chart.
- Use **Iterate** to run forward to an absolute simulation time at maximum CPU
  speed while showing a progress bar plus global/instantaneous speed
  multipliers; it runs the same simulation ticks and deferred spawn/kill work as
  the normal pond.
- The HUD `Spikes` counter is the total number of creatures killed by evolved
  radial strikes.
- Speed 1x/2x/4x/8x; auto-save every 60 seconds while running.

There is no direct creature control and no hard fail state: the pond is an
experiment. Metabolism, movement, and reproduction are deliberately costly, so
creatures that only spin in place eventually starve; successful lineages must
find food. If every creature dies, the Extinction panel offers a new pond or an
earlier save.

## Controls

| Input | Action |
|-------|--------|
| Main menu | Continue / New Game / Load Game / Quit |
| New Game | Seed, mutation preset, food preset, collision off/soft, early bloom off/on |
| Click creature | Open genome/stats inspector |
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
- mutation, metabolism, reproduction, ageing, lineage threshold;
- spike threshold/cost/radius/cooldown;
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

## Genome

Every creature carries the same fixed network layout:

```
8 sensors → 5 mixed hidden nodes → 5 mixed hidden nodes → 3 actions (tanh)
```

Sensors:

1. food ahead
2. food left
3. food right
4. wall ahead
5. other creature ahead
6. own energy
7. `sin(clock)` rhythm
8. `cos(clock)` rhythm

Actions:

1. `moveDrive` (forward when above threshold)
2. `turnDrive` (turn rate)
3. `spikeDrive` (predatory radial strike; consumes energy and kills nearby
   creatures, then enters a cooldown)

The two hidden layers use per-node activation-type genes. A node can be:

- `sig` — sigmoid
- `sum` — pass the weighted sum through unchanged
- `inv` — negate the weighted sum (inhibitory relay)
- `relu` — rectified linear

The genome is 103 hexadecimal digits:

| Segment | Hex digits |
|---------|-----------:|
| sensor → hidden 1 weights | 40 |
| hidden 1 activation types | 5 |
| hidden 1 biases | 5 |
| hidden 1 → hidden 2 weights | 25 |
| hidden 2 activation types | 5 |
| hidden 2 biases | 5 |
| hidden 2 → action weights | 15 |
| action biases | 3 |

A weight digit is 0 (no edge), 1–7 (positive), or 8–F (negative); multiple
incoming edges are summed before the bias and the node's activation. The rhythm
sensors use each creature's own age, not a shared pond clock:
`phase = age * 2π / 6s`, so every creature starts its sine/cosine at birth and
creatures born at different times are desynchronised. Mutation changes each digit
independently at the selected rate (0.5%, 1.5%, or 3.0%); a child may be identical
to its parent. A birth with at least four mutated digits starts a new visible
lineage.

Spiking is a real evolved action, not a scripted effect: when `spikeDrive` rises
above the fixed `Tuning.SpikeThreshold` (0.60; not a genome value), the creature
spends `SpikeEnergyCost`, kills **every other creature inside `SpikeRadius`**, and
enters a `SpikeCooldownSeconds` cooldown. Calibrated founders start with a silent
spike bias so the first generation is not automatically predatory; mutation can
evolve it. A successful strike draws an expanding red ring/spike flash.

Founder genomes start from a **calibrated starter brain**, not a random network.
The template explicitly leans into food, turns toward food seen on either side,
turns away from a wall ahead, and keeps a weak rhythm-driven wander. Each founder
then gets a broader-than-usual mutation pass (10% of its digits) and a restored
forward bias, so the starting population is diverse but can still forage. This
is the cold-start fix: evolution gets working material instead of having to
rediscover locomotion and steering from scratch.

An **optional food bloom** can be enabled for the first three minutes; it starts
with a higher food target/spawn rate and ramps down to the selected abundance. It
is off by default. Each founder also starts with a small nutrient cluster nearby,
and a dying creature drops a small amount of food (detritus) so an early bad
generation can feed the survivors instead of ending in a dead pond. Food spawns
are clamped inside the pond boundary, including starter snacks and detritus.

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

The inspector shows the theme, pattern, and body hex color for the selected
creature. The Diversity board derives its metrics from genomes through the same
appearance logic.

## Simulation

The SND level `pond` contains one `world_controller` entity. It is also the only
entity with a strategy (`evolution.world`): it advances a fixed 10 Hz simulation,
reads all creature/food entity data, and schedules reproduction, food spawning,
and death through Origo's deferred queue. Creatures and food are SND entities with
empty Godot placeholder nodes; `EvolutionWorldView` renders the pond directly
from their TypedData, which keeps hundreds of creatures cheap to animate.

Food is deliberately scarce overall, but it is generated as **clustered
settlements** rather than uniform noise: 16 deterministic patch centers receive
initial and manually-fed food, and each patch is driven by its own SND food-source
entity. Within each patch the radial density is graded as
`(1 - r/R)^falloff` (`falloff = 1.8` by default): food is densest at the core and
smoothly thins toward the rim, with a sparse halo instead of a hard uniform-disk
edge. Patch centers are derived from the pond seed, so save/load preserves the
layout. The food target is about 4,000 over the 10,000-radius pond; movement and
metabolism are expensive enough that an orbiting creature that never finds a
patch will starve, and the 3,000-creature cap remains a safety valve.

### Adaptive SND food sources

Each patch owns a `food_source_<n>` entity with the `evolution.foodsource`
strategy. Once per second it scans food near its current position and globally;
every half second it attempts a small spawn burst. The spawn chance is
`(1 - localFood/localCapacity) * cycleFactor`, so food-rich patches stop producing
while empty patches recover. Each source has a local capacity of 300 food,
keeping unattended patches from consuming the global hard cap and starving
occupied ones.

Sources also **randomly walk** through the pond in 0.5-second steps at
4–16 world-units/second, with a random heading and boundary steering. Their food
halos therefore migrate rather than staying at fixed coordinates. Source state
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
exposes the 3,000 hard cap so it is visible when the safety valve is approached.

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
  Scripts/
    DemoEntry.cs                # OrigoDefaultEntry, paths, save metadata
    DemoUi.cs                   # main menu, HUD, inspector, pause/save/load
    DemoSmokeRunner.cs          # optional headless acceptance runner
    EvolutionWorldView.cs       # pond/creature/food drawing
    EvolutionEntityFactory.cs   # shared creature/food spawn metadata
  Strategies/
    WorldSimulationStrategy.cs  # simulation, mutation, controller state
    FoodSourceStrategy.cs       # adaptive per-patch food production
    OvergrowthStrategy.cs       # global high-density mutation pressure
```

`Origo.EvolutionDemo.Logic` contains the Godot-free genome, brain, sensor,
appearance, food-patch, diversity, and central `DemoSettings` layer; its tests
are in `Origo.EvolutionDemo.Logic.Tests`.

## Known first-version limits

- One pond, no predators, no combat.
- Fixed network topology and point mutations only; no crossover or variable
  layer/node counts.
- No offline progression; the simulation runs while the game is open.
- Procedural vector visuals only; no external art/audio assets.
- UI text is English.
