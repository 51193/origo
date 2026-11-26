# Campaign Player-Model Baselines — 2026-11-25

> [↑ Back to demo Docs](README.md)

## Problem

The first campaign autoplay was not a player model. It bought **every affordable
unlock on every simulation tick**, used a fixed control order, and inherited
points/milestones from the 10 s smoke/load phase before the benchmark pond
started. It had no purchase log, so a run could show a crash or a population
plateau without telling us whether the player ever had to choose, save, or wait.

That is a useful "buy everything immediately" control, but it overstates early
progress and hides the most important campaign decision: **when to spend
Evolution Points, and on what**. The previous long-run data still describes the
old control accurately; it should not be read as a universal player experience.

## Thinking

Campaign players differ mostly in two dimensions:

1. **Decision cadence** — do they check the shop constantly, or return after a
   long idle period and spend a batch?
2. **Target discipline** — do they save for the next capacity/survival goal, or
   buy whatever is currently cheapest?

That gives at least three representative models worth testing:

- a **goal-oriented** player who saves and buys one planned target at a time;
- an **idle/returning** player who visits rarely and buys everything affordable
  in one session;
- a **casual/collector** player who buys the cheapest available ability often,
  without saving for capacity.

The greedy per-frame buyer remains useful as a **development control**, not as
a baseline player.

To make the comparison meaningful, every model must start from the same state
and buy through the real gates:

- fresh profile with zero points and only the core unlock;
- no purchases during smoke/load;
- one policy decision at a time (one purchase for planned/cheapest, a batch for
  idle, everything for greedy);
- only purchases that satisfy cost, prerequisites, and milestones;
- a purchase log with time, cost, and points before/after;
- the same seeds, horizon, and checkpoint cadence.

## Decision

### Player models

| Strategy | Player model | Cadence | Shop behavior |
|---|---|---:|---|
| `planned` | Goal-oriented optimizer | 10 s | Save for the next plan target; wait if it is unaffordable or milestone-locked |
| `idle` | Idle/returning player | 300 s | At each visit, buy every affordable core ability in plan order |
| `cheapest` | Casual collector | 15 s | Buy the cheapest currently unlockable core ability |
| `greedy` | Development control | every tick | Buy every affordable ability in the control order |

`cheapest` is restricted to the core campaign unlock list so optional content
packs cannot become the cheapest purchase and distort the baseline.

### Planned order

The planned and idle models use a capacity/survival-first order:

```text
tough food, hard digest, pond 1, sprint,
toxic food, toxin resistance, pond 2, corpse cycle, carrion sense,
pond 3, pond 4, food-type vision,
predation, battery, armor,
pond 5, pond 6,
creature sense, rhythm, enrichment
```

The order is a test-player decision, not a claim about the optimal build. It
deliberately delays optional senses and enrichment so the early campaign is not
spent on side grades before capacity.

### Run design

- Horizon: 3,600 simulation seconds; checkpoint every 300 s.
- Seeds: `20261101`, `20261132`, `20261133`.
- Three selected models × three seeds; greedy control on `20261101`.
- Fresh profile per run; `ORIGO_DEMO_SMOKE_SEED` set explicitly.
- Economy and ecology read from the final save plus 600/1500/1800/3600
  checkpoints; the last two checkpoints are analyzed together to obtain the
  3300→3600 kill/death interval.
- Full artifacts live in the ignored `.scratch/` run directory; the compact
  committed record is
  [`baselines/campaign-2026-11-25.json`](baselines/campaign-2026-11-25.json).

## Result

### Population trajectory

| Strategy | Seed | 600 s | 1,500 s | 1,800 s | 3,600 s | Peak |
|---|---:|---:|---:|---:|---:|---:|
| planned | 20261101 | 900 | 1,599 | 1,600 | 201 | 1,600 @ 1,800 |
| planned | 20261132 | 898 | 1,599 | 1,597 | 1,663 | 1,689 @ 3,300 |
| planned | 20261133 | 899 | 1,598 | 1,549 | 341 | 1,598 @ 1,500 |
| idle | 20261101 | 120 | 120 | 250 | 244 | 314 @ 3,300 |
| idle | 20261132 | 120 | 119 | 120 | 226 | 264 @ 3,300 |
| idle | 20261133 | 120 | 120 | 250 | 264 | 348 @ 3,300 |
| cheapest | 20261101 | 250 | 298 | 147 | 267 | 298 @ 1,500 |
| cheapest | 20261132 | 250 | 230 | 247 | 282 | 309 @ 3,300 |
| cheapest | 20261133 | 250 | 171 | 148 | 245 | 250 @ 600 |
| greedy control | 20261101 | 391 | 710 | 189 | 259 | 710 @ 1,500 |

### Final envelopes

| Model | Final pop | Founder share | Gene count | Sense / 300 access | Diet entropy / >60% | Predation last interval | First / last purchase | Purchases |
|---|---:|---:|---:|---:|---:|---:|---|---:|
| planned (3 seeds) | 201–1,663 (median 341) | 0–18.4% | 49.5–64.2 | 45–78% / 59–90% | 0.920–0.930 / 7.8–10.8% | 30–102% | 10.1 s / 2,046–3,457 s | 19–20 |
| idle (3 seeds) | 226–264 | 12.4–16.8% | 52.3–55.8 | 59–72% / 80–96% | 0.935–0.950 / 2.6–7.1% | 102–104% | 300.1 s / 2,401–2,701 s | 19 |
| cheapest (3 seeds) | 245–282 | 13.8–15.4% | 51.8–68.1 | 48–71% / 65–84% | 0.933–0.951 / 7.7–11.1% | 100–106% | 15.1 s / 1,809 s | 19 |
| greedy control (1 seed) | 259 | 14.3% | 54.9 | 78% / 93% | 0.934 / 7.7% | 103% | 0.1 s / 1,799 s | 19 |

`predation last interval` is `kills / estimated deaths`. When enrichment is
active, injected founders are not counted in `evo.births`; estimated deaths are
therefore a **lower bound**, and predation share an **upper bound**. The planned
20261132 run never bought
enrichment (founder share 0) and reports 30%; that one is a clean lower-noise
reading.

### Purchase pacing

| Model | Pond 1 | Pond 2 | Pond 3 | Pond 4 | Pond 5 | Pond 6 |
|---|---:|---:|---:|---:|---:|---:|
| planned | 30–40 s | 81–331 s | 111–361 s | 141–381 s | 1,056–1,198 s | 1,955 s |
| idle | 300 s | 1,500–1,800 s | 1,800–2,100 s | 1,800–2,100 s | 2,100 s | never |
| cheapest | 241–376 s | 346–541 s | 601 s | 616 s | 1,809 s | never |
| greedy control | 6 s | 25 s | 600 s | 1,499 s | 1,799 s | never |

The planned player reaches the high-capacity ponds very early; the idle and
cheapest models reach pond 1 much later and never reach pond 6. The greedy
control reaches pond 2 in 25 s but then stalls for a long time.

## What the baseline changed in our understanding

### 1. Strategy, not just seed, controls the early campaign

At 600 s the planned model is at ~900 creatures on all three seeds, the
cheapest model at 250, idle at 120, and greedy at 391. At 1,500 s planned is
~1,600 while idle is still ~120. A benchmark without a stated player model is
therefore not a benchmark of "the campaign"; it is a benchmark of one spending
policy.

### 2. The planned model can settle high or collapse; the end state is bimodal

All three planned seeds reach a similar high-density state (~1,550–1,600 at
1,800 s), but finish at **201, 1,663, and 341**. The high-density state is easy
to reach; whether it persists is seed-sensitive. The earlier "campaign always
crashes to a few hundred" was a property of the greedy policy plus its smoke
head start, not a law of the game.

The planned 20261132 run also differs in content: it never bought enrichment,
kept founder share at 0, reached 64 gene copies, and stayed at 1,663 with a 30%
predation share. The other two planned runs bought enrichment late and finished
at 201/341 with founder shares 18.4%/13.2%. This is a **hypothesis to test**, not
a proven cause: enrichment timing, founder injection, food supply, and seed
all move together.

### 3. Idle and cheapest players stabilize low

The idle model spends its first points at 300 s and only reaches pond 2 around
1,500–1,800 s. It stays at 119–120 until 1,500 s, then settles at 226–264.
The cheapest model starts at 250, oscillates between 147 and 298, and settles at
245–282. Neither shows the planned model's boom/bust; their end states are
close to each other and to the greedy control's 259.

For this game, a low-density, stable campaign is the default experience for a
player who does not aggressively buy capacity. The high-density state requires
a capacity-first plan and survives only on some seeds.

### 4. The old crash is still a real control-path failure

The greedy control peaks at 710 at 1,500 s and falls to 189 at 1,800 s, then
settles at 259. That is the familiar 1,500→1,800 collapse. It remains useful as
a regression control, but it should not be the only campaign baseline.

### 5. The realistic test found a food-cap defect

The first fresh planned probe at 120 s failed the smoke cap assertion with
**food 715 > cap 700**. Food sources used a stale scan count and did not account
for corpse/feed food pending in the same tick. The source now uses the current
tick's food count and includes the corpse pending food. All ten baseline runs
complete without a cap violation. This is a game fix, not a relaxed test
tolerance.

### 6. Cluster structure is still seed-specific

Final cluster solutions range from k=2 to k=8; silhouette 0.14–0.39. Planned
20261132 gives a clean k=2 (silhouette 0.292, bootstrap ARI 1.0), while idle
20261101/32 give k=7 and cheapest 20261101 gives k=8. The earlier strong
two-ecotype claim is a seed-specific outcome, not a campaign invariant.

## Selected baselines

We keep three player models and one control:

| Role | Model | Why |
|---|---|---|
| Engaged, goal-oriented | `planned` | Covers capacity-first play, high-density boom/bust, and the high stable outcome |
| Idle/returner | `idle` | Covers long decision gaps and batch shopping; distinct slow early trajectory |
| Casual/collector | `cheapest` | Covers frequent low-cost purchases without saving; low stable end state |
| Development control | `greedy` | Preserves the old buy-everything comparison; not a player baseline |

The three selected models cover the meaningful spread: **high growth then
bimodal end state**, **slow low growth**, and **frequent side-grade spending**.
They should be compared as envelopes, not exact golden numbers.

## Follow-up: six-seed strategy matrix

The [six-seed matrix](campaign-matrix-2026-11-25.md) extends the planned
envelope to nine runs. Only one of nine planned runs held the high-density
state; the matrix clarifies that the 1,663 case is a rare survivor, not the
expected planned outcome. Use the matrix path envelope for planned and retain
this page as the first-baseline record. The
[controls follow-up](campaign-controls-2026-11-26.md) shows that enrichment
and predation are jointly necessary for the planned collapse.

## How to use and refresh the baseline

- Use the same seeds, 3,600 s horizon, 300 s checkpoints, fresh profile, and
  explicit strategy. A result outside the envelope is a discussion point, not
  automatically a regression.
- Compare the **purchase timeline** first: if pond/unlock timing moved, the
  population difference is probably a cause, not an unexplained balance drift.
- When a change is intentional, add a new dated JSON baseline and update this
  chapter; keep the old file and note what superseded it.
- Do not use a single greedy run as the campaign baseline.

### Reproduce one run

```bash
ROOT=/path/to/origo
STRATEGY=planned
SEED=20261101
INTERVAL=10

EVOLUTION_DEMO_SMOKE_HOME=/tmp/campaign-$STRATEGY-$SEED \
ORIGO_DEMO_SMOKE_SEED=$SEED \
ORIGO_DEMO_CAMPAIGN_STRATEGY=$STRATEGY \
ORIGO_DEMO_CAMPAIGN_FRESH=1 \
ORIGO_DEMO_CAMPAIGN_PURCHASE_INTERVAL=$INTERVAL \
ORIGO_DEMO_CAMPAIGN_PURCHASE_LOG=/tmp/campaign-$STRATEGY-$SEED-purchases.csv \
  bash "$ROOT/samples/Origo.EvolutionDemo/run-demo.sh" --no-build --headless \
  --seconds 2 --fast-forward 3600 --checkpoint-interval 300 --save-slot final
```

The `iterate_*` saves are under
`$EVOLUTION_DEMO_SMOKE_HOME/.local/share/godot/app_userdata/Origo Evolution Demo/evolution_demo_saves`
on Linux. Analyze the last interval with:

```bash
dotnet run --project samples/Origo.EvolutionDemo.Analysis -c Release -- \
  genome \
  --save <saves>/save_iterate_3300 \
  --save <saves>/save_final \
  --timeline-csv /tmp/final.timeline.csv \
  --metrics-json /tmp/final.metrics.json
```

## Caveats

- 3,600 s with three seeds is a screening baseline, not a full distribution.
  The planned model's bimodal end state needs more seeds to estimate its odds.
- This is still an automated player: it does not read the UI, change plans
  after a crash, or deliberately under-spend. The values are hypotheses for
  human playtesting.
- Predation share is an upper bound whenever enrichment is active (the death
  estimate is a lower bound); use direct game counters when they exist.
- Founder share counts injected generation-0 creatures; it is not a lineage
  survival rate.
- The plan order is a chosen player model. A different goal-oriented order
  could produce a different high-density outcome.
