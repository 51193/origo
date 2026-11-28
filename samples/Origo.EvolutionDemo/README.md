# Origo Evolution Demo

A playable idle artificial-life sandbox built on `Origo.Core` and
`Origo.GodotAdapter`. Design/playtest notes and archived player-model baselines
live in [Docs](Docs/README.md).

## What you do

- Start a **Campaign** pond from the main menu (24 primordial founders in a
  700-unit pond, basic Vision and a minimal feeding reflex) or load a saved
  lineage. Campaign play unlocks food types, organs, senses, death/carrion,
  predation, and pond expansions with Evolution Points. **Sandbox** mode in the
  New Pond panel keeps the full 900-founder, 10,000-unit ecosystem.
- Sandbox/benchmark mode runs the full content and the old 900-founder/10,000-unit
  setup. Creatures are deliberately small at that scale, so the local view
  reads as a sparse world; Campaign starts intentionally tiny.
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
  a hit deals energy damage, and the parenthesized `K` value tracks lethal hits.
- Speed 1x/2x/4x/8x; auto-save every 60 seconds while running, and optional
  checkpoint snapshots every N simulated seconds during timed iteration.

There is no direct creature control and no hard fail state: the pond is an
experiment. Metabolism, movement, and reproduction are deliberately costly, so
creatures that only spin in place eventually starve; successful lineages must
find food. Sprinting multiplies distance by 1.6x but adds a movement-proportional
energy surcharge, so it is a trade-off rather than a free speed boost. If every creature dies, the Extinction panel offers a new pond or an
earlier save.

## Running

The repository script builds and launches the demo, or runs its headless
acceptance smoke:

```bash
# Build and launch the windowed demo.
bash samples/Origo.EvolutionDemo/run-demo.sh

# Skip the build and launch the already-built demo.
bash samples/Origo.EvolutionDemo/run-demo.sh --no-build

# Headless smoke with a chosen simulated target.
bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 120

# 1h genome screening benchmark in the isolated smoke save home.
bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 5 \
  --fast-forward 3600 --save-slot bench_1h

# 1h benchmark with a snapshot every 5 simulated minutes (iterate_300 ...
# iterate_3300 plus the final bench_1h slot).
bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 5 \
  --fast-forward 3600 --checkpoint-interval 300 --save-slot bench_1h
```

The windowed runner stores Godot user data in `.godot-home/`; headless smoke
uses a separate `.godot-home-smoke/`, so test runs can never overwrite player
saves. Override them with `EVOLUTION_DEMO_HOME` and
`EVOLUTION_DEMO_SMOKE_HOME` respectively. The runner prints the active user-data
directory on startup. `scripts/bash/godot-test.sh` runs a short campaign autoplay
smoke after the Godot integration tests, so cap, camera-fit, checkpoint
provenance, save/load, and reset assertions are exercised by the Godot CI gate.
Windowed saves live under:
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
- food amounts, food caps, feeding, bloom parameters; food-per-second
  spawning applies only when the patch count is zero (patches produce their own
  food);
- body gene ranges for size, speed, turn, sense, spike, metabolism, age, body
  radius, pickup padding, and max-energy scaling;
- spike salvo base/per-pellet/distance cost and cooldown per extra pellet;
- body, brain, and body-length mutation rates (they scale the typed V4
  mutation operator mix), metabolism (including the configurable genome-upkeep
  share of the V4 energy budget, per-organ upkeep, the sprint surcharge
  fraction, and the armor mobility multiplier), reproduction, ageing, and the
  lineage divergence threshold (lineage splits when a child differs in at least
  N genes);
- spike threshold, base cost, cost per world-unit, aim half-angle, beam radius,
  damage fraction, kill steal fraction, armor damage multiplier, range, and
  cooldown;
- carcass food interval, feeding amount/cooldown, and related world settings;
- tough/toxic food shares and their energy values;
- every food-patch/source parameter: count, radius, falloff, local capacity,
  scan/spawn/walk cadence, walk speed/turn, and all long-cycle bounds;
- global overgrowth scan interval, cell size, food threshold, mutation rate,
  consumed-food cost, and child energy;
- timed-iteration checkpoint interval (0 disables automatic iterate snapshots).

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

**Iterate checkpoints.** When the advanced setting **Iterate checkpoint (s,
0=off)** is positive, timed iteration pauses at every absolute multiple of that
interval and writes a snapshot before continuing. A checkpoint uses the save ID
`iterate_<seconds>` (for example `iterate_300`), the same Origo save pipeline
and display metadata as a manual save, and its recorded `evo.time` is the first simulation tick at or after the
checkpoint mark (at most one tick later), so a long iteration produces a
reproducible time series
without the player saving by hand. Checkpoints are ordinary save slots and are
shown in the Load Game panel; interval values below 30 simulated seconds are
rejected instead of flooding the save root, and the final target time is never a
checkpoint (the normal end-of-run save owns that state). The headless runner
exposes the same interval as `--checkpoint-interval` /
`ORIGO_DEMO_SMOKE_CHECKPOINT_INTERVAL` and verifies that every expected
checkpoint exists after the iteration.

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
`game.milestones`, and `game.lifetimeSeconds` for save snapshots.
`GameplayPersistence` is the single boundary for that per-box mirror; there is no
process-wide profile file. Unlocks, points, and milestones belong to the box and
its save, so New Game and a new box start from their own profile. Save version 7
rejects older saves. Evolution Points start at zero in Campaign, milestones and
the passive trickle award them, and the unlock shop spends them.

Press `U` while a pond is running to open the generic unlock shop. Rows are
generated from `UnlockDefinition` data, not from a hardcoded organ list. Buying
an unlock writes the progression back to the controller immediately.

## Campaign progression

`New Pond` defaults to **Campaign — primordial start + unlocks**; choose
**Sandbox — all genes and mechanics** to keep the unrestricted ecosystem used
by benchmarks and mechanics experiments. The foreground pond and the background
meadow are separate box sessions with separate runtimes, controllers, profiles,
and save payloads.

A 斗蛐蛐 arena is not a separate simulation type: it is a normal box created
with the all-enabled profile and a manually seeded initial population. It uses
the same reproduction, food, collision, migration, save, and analysis paths as
every other box. Termination conditions are deliberately not implemented yet.

While a pond is running:

| Key | Action |
|---|---|
| `G` | open the ecology-box panel (optional; use `Eco Boxes`) |
| `B` | switch the foreground to the next ecology box |
| `N` | create a new all-enabled box |
| `Shift+N` | create a no-predation “forage” box |
| `M` | migrate the selected creature to the next box |

The HUD `Eco Boxes` button opens the panel. It can switch foreground, pause/resume individual boxes, pause or
resume all boxes, delete background boxes, create sandbox/forage boxes, and
create an empty manual-start box. `Seed` copies the currently selected
foreground creature into a box and `Move` transfers it. All box operations can
be reached with the mouse through the `Eco Boxes` HUD button; the keyboard
shortcuts are optional accelerators.

`BoxMigration` can move a creature from one box to another. The destination
profile must allow every expressed organ, action, and sensor; an incompatible
destination rejects the migration instead of recompiling or disabling traits.
A migrant keeps its genome, phenotype, energy, age, lineage, root lineage, and
meal/kill counters. Each box persists a disjoint `box.rootBase`, so int root
ids from different boxes do not collide; `evo.rootOriginBox` preserves the
origin label for migration and reporting.

A fresh box's first Campaign starts with:

- 24 founders in a 700-unit radius pond (population cap 64, three food patches);
- only Vision, `FoodAhead/Left/Right`, `WallAhead`, `Energy`, `Move`, `Turn`, and
  a minimal food reflex;
- soft food only, instant-food death handling, no spike/armor/carrion/organ
  senses.

Evolution Points (EP) come from the box's milestones and a passive trickle
(1 EP per 30 simulated seconds); they persist in the box save. The unlock
tree is gated by prerequisites, milestone requirements, and EP cost:

| Tier | Typical unlocks |
|---|---|
| Foraging | tough food, HardDigest, Sprint, pond I |
| Chemistry | toxic food, ToxinResistance, CreatureSense, Rhythm, pond II |
| Carrion | corpse-pulse death, CarrionSense, pond III |
| Predation | SpikeOrgan/spike action, battery, armor, pond IV |
| Vision | FoodTypeVision + six food sensors, enrichment spawning, pond V |
| Frontier | pond VI |

Food composition also stages with the unlocks: soft only → tough 15% → tough
25%/toxic 15% → final tough 40%/toxic 40%. Unlocking an organ seeds up to three
carriers of the new organ immediately so a purchase is visible without waiting
for a random mutation (the population cap can reduce the count). Pond expansions apply their radius, population cap, food
cap/rate, and additional food sources to the active pond. Campaign profiles use
schema version 2; old schema-1 placeholder profiles are quarantined and start
fresh rather than carrying the old placeholder point balance.

For automated playtesting, the smoke runner starts Campaign and uses a named
**player model** to spend Evolution Points. A fresh benchmark starts from zero
points/unlocks (the 10 s smoke/load phase no longer buys anything), purchases
are applied through the normal unlock gates, and every purchase is logged:

```bash
ORIGO_DEMO_CAMPAIGN_STRATEGY=planned \
ORIGO_DEMO_CAMPAIGN_FRESH=1 \
ORIGO_DEMO_CAMPAIGN_PURCHASE_LOG="$PWD/planned-purchases.csv" \
bash samples/Origo.EvolutionDemo/run-demo.sh --no-build --headless \
  --seconds 2 --fast-forward 3600 --checkpoint-interval 300 --save-slot campaign
```

| Strategy | Player model | Default interval |
|---|---|---|
| `planned` | Goal-oriented: save for the next capacity/survival target, one purchase per decision | 10 s |
| `idle` | Idle/returning: visit the shop at long intervals and buy everything affordable | 300 s |
| `cheapest` | Casual: buy the cheapest currently available core ability | 15 s |
| `greedy` | Development control: buy every affordable ability on every tick | every tick |

`ORIGO_DEMO_CAMPAIGN_AUTOPLAY=1` remains a compatibility alias and now maps to
`planned`. `ORIGO_DEMO_CAMPAIGN_FRESH=0` preserves the old smoke-earned
points/milestones for comparison. Controlled experiments can set
`ORIGO_DEMO_CAMPAIGN_ENRICHMENT=0` and/or
`ORIGO_DEMO_CAMPAIGN_PREDATION=0`; the policy then skips that mechanic's
unlocks while leaving the rest of the plan intact. The normal smoke waits up
to 30 simulation seconds for generation 1 (the second generation) before declaring the seed unusable.
`DEMO_CAMPAIGN_PURCHASE` lines record
`time,strategy,unlock,cost,points_before,points_after`; `DEMO_CAMPAIGN` adds the
strategy, purchase count, and first/last purchase time. The plan order and the
selected baseline data are documented in [Docs/campaign-baselines.md](Docs/campaign-baselines.md).

The interactive windowed run is the normal `run-demo.sh` command; Campaign
mode is selected by default in the New Pond panel. Use a fresh
`EVOLUTION_DEMO_SMOKE_HOME` for each strategy comparison: the account profile
persists unlocks and points across runs by design.

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
  This is a compiler/tooling capability; the playable runtime currently compiles
  with a neutral context (see the limits below).
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

The controller stores `evo.genomeVersion = 7`. Older saves are intentionally
not migrated; the Load panel rejects them with an explicit message. Genome
serialization is a canonical binary codec stored as prefixed base64 in SND
entities (about a third smaller than the legacy hex form), with a hex view for
debugging and the inspector.

Mutations operate on stable gene identities: expression, copy count,
duplication/deletion, payload parameters, neurons, synapses, organs, and
regulatory links. A mutation is accepted only if the resulting genome validates
and compiles, so random search stays in viable phenotype space.

The detailed architecture and mock benchmark are in
[`Origo.EvolutionDemo.Logic/V4/README.md`](../Origo.EvolutionDemo.Logic/V4/README.md).

### Food types and digestive specialization

Food sources can produce three food categories: soft, tough, and toxic. All
bodies can eat every category, but tough food yields full energy only with the
`HardDigest` organ and toxic food yields full energy only with the
`ToxinResistance` organ. Without the matching organ, the food is still edible at
reduced value, so a new food type cannot starve a population before digestion
mutations appear. Founder bodies start with a mix of battery, hard-digest, and
toxin-resistance organs.

A body expresses only one digestive specialization at a time. When both organ
genes are active, the expressed gut with the larger total organ size keeps its
full value and the other specialization is suppressed; an equal total prefers
hard digest. Digesting both food types at full value therefore requires a real
specialization choice instead of accumulating two unconditional upgrades.

Foraging picks the food item with the highest effective energy inside the
pickup radius (ties prefer the nearer item, then the lower entity index).
A hard-digest body therefore prefers tough food, a toxin-resistant body prefers
toxic food, and a body with neither prefers soft food. This turns the existing
food categories and digestive organs into a diet specialization with measurable
niches instead of a passive energy multiplier.

Each creature also records its own `evo.creatureSoftMeals`,
`evo.creatureToughMeals`, `evo.creatureToxicMeals`, and
`evo.creatureCorpseMeals`, which the analysis tool exposes as `behavior.*Meals`
and `diet.*` features.

The heritable `FoodTypeVision` organ exposes three omnidirectional type
sensors — `FoodSoft`, `FoodTough`, and `FoodToxic` — plus three directional
value sensors — `FoodValueAhead`, `FoodValueLeft`, and `FoodValueRight`. The
type sensors report the best proximity of each food type in sense range
(corpse food counts as soft). The value sensors report proximity multiplied by
the food's effective energy for the creature's expressed gut, so the same food
looks more valuable to its matching specialist. The sensors do not encode an
absolute preference; founder carriers receive a simple value-following reflex
scaffold, and neural mutation can rewire or override it, leaving the
combination of sensing, digestion, and locomotion to selection.

Tough and toxic food each supply 40% of new food-source spawns (soft 20%);
startup, corpse, and manual-feed food stay soft. This lets specialists find
their preferred diet; the gutless founder cohort is small and selection
favours the sensory organ in the tested runs. The organ has the normal organ
upkeep cost and is carried by a minority of founders; the organ mutation
operator can also create it, so a lineage without carriers can evolve it.

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

The spike is no longer a radius AoE. The body's `SpikeOrgan` gene enables the
action, and the `SpikeRange` life trait is the maximum shot distance. A
`SpikeBattery` organ (and its copy count) turns the action into a salvo of rays;
half of calibrated founders carry the battery. The V4 neural graph's `Aim`
output port supplies a signed aim offset. The current adapter uses a fixed 45°
spread and zero jitter; only the battery copy count comes from the body:

```text
aimAngle = bodyHeading + aimDrive * SpikeAimHalfAngle
```

Each pellet tests creatures inside a ray/beam and picks the nearest valid
target not already hit by this salvo. A hit deals `SpikeDamageFraction`
(default 0.5) of the target's maximum energy as damage, and the target dies when
its energy reaches zero. Only a lethal hit transfers `SpikeKillStealFraction`
(default 0.5) of the target's remaining energy to the shooter; non-lethal hits
deal damage without feeding the shooter, so repeated hits cannot farm energy.
Only a lethal hit spawns the normal corpse/death-food amount at the target
position. The `Armor` organ scales incoming spike damage by
`SpikeArmorDamageMultiplier` (default 0.4), so an armored body survives more
hits; armor is available to founders and mutations. Armor is a mobility
trade-off: an armored body moves and turns at `ArmorSpeedMultiplier` (default
0.9) of its expressed speed. The defaults make predation
a routine part of the pond rather than a rare event: a low `0.05` drive
threshold, a wide 32-unit beam, a 1.5 s cooldown, and a cheap base cost. Energy cost is:

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
patch will starve, and the 2,500-creature cap remains a safety valve. The
compiled V4 energy budget (body tissue, neural tissue, and genome complexity) is
charged as a per-second upkeep; the Advanced Settings "Genome upkeep / budget"
scale controls it (0 disables the charge). Each distinct expressed organ type
also costs `OrganUpkeepPerSecond` (default 0.06) so carrying a broad organ
loadout trades against metabolism. Sprint applies a 1.6x movement multiplier
and charges an extra fraction of the driven move cost
(`SprintSurchargeFraction`, default 2.5), so the sprint option costs more per
distance than walking instead of being a free speed increase.

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
  high populations; the batch phase comes from each creature's stable id, so a
  death or spawn elsewhere cannot shift another creature's update cadence;
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
Timed-iteration checkpoints are ordinary slots written through the same pipeline
and appear next to manual saves in the Load Game panel.

## Project layout

```
samples/Origo.EvolutionDemo/
  Main.tscn                     # one root Node: DemoEntry
  Origo.EvolutionDemo.sln       # Godot C# solution used by editor builds and export
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
- Regulatory conditions are evaluated by the V4 compiler, but the playable
  demo compiles with a neutral context; regulation is not yet a live
  food/predator/energy response.
- Neural topology is gene-expressed within safety and energy caps; no fixed
  latent lattice.
- Soft collision uses each creature's body radius; it remains off by default.
- No offline progression; the simulation runs while the game is open.
- Procedural vector visuals only; no external art/audio assets.
- Saves written before the digestive specialization rule keep their persisted
  organ mask: a loaded creature can carry both HardDigest and ToxinResistance,
  while newly born creatures express at most one digestive specialization.
- UI text is English.
