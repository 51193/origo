# Genome v4

Genome v4 is the runtime genome of the Evolution Demo. It replaces the fixed
body-codon and 372-hex neural genomes with a stable-ID gene graph.

## Structure

- **Gene list with stable IDs** — genes are records with `Id`, `Kind`, copies,
  expression, payload, and regulatory conditions. IDs are never renumbered by
  mutation; new gene types are decoded by their own payload codec.
- **Canonical binary codec** — deterministic ordering, explicit schema version,
  strict validation, and a hex view for debugging. It is not an opcode table.
- **BodyPlan graph** — segments and arbitrary organ instances. `CopyCount`
  expands organs without a fixed slot array.
- **NeuralGraph** — sensor/interneuron/output nodes and synapse genes. There is
  no fixed hidden-layer array, 372-hex matrix, or fixed sensor/output slot
  count.
- **Regulatory expression** — genes can require `FoodLow`, `PredatorNear`, or
  `EnergyLow`; the same genome compiles to different bodies under different
  contexts.
- **Energy budget** — body tissue, brain tissue, and genome complexity all
  contribute maintenance cost. A reflex-only brain is cheap; a large graph brain
  costs more.
- **Transactional mutation** — a mutation is accepted only if the resulting
  phenotype's expressed sensor/action ports also pass the content gate; the
  genome validates and compiles; otherwise the parent is returned. This keeps
  random evolutionary search in viable-phenotype space.

## Mock benchmark

Run:

```bash
source scripts/bash/dotnet-env.sh
dotnet run --project samples/Origo.EvolutionDemo.GenomeV4.Bench --configuration Release
```

Measured results:

| variant | genes | neurons | synapses | organs | maintenance/s | codec bytes |
|---|---:|---:|---:|---:|---:|---:|
| simple reflex | 3 | 2 | 1 | 1 | 2.461 | 91 |
| graph, 32 hidden | 131 | 34 | 95 | 1 | 10.337 | 3927 |
| large body, 512 organs | 17 | 0 | 0 | 512 | 321.5 | 439 |
| large logic brain, 32 redundant | 41 | 36 | 3 | 1 | 7.697 | 1129 |

Non-linear AND task:

| controller | accuracy |
|---|---:|
| direct reflex only | 50% |
| variable graph with hidden node | 100% |

Mock energy economy (18 energy per correct decision, 3600 s):

| opportunities/s | reflex net/s | large-brain net/s |
|---:|---:|---:|
| 0.2 | -0.028 | -0.030 |
| 0.5 | 1.977 | 1.458 |
| 1.0 | 6.412 | 10.303 |

So the cheap reflex wins at medium opportunity rates, while the large brain
wins in the rich environment; both are slightly negative at the lowest rate.

Regulatory test: the conditional battery genome expresses 1 battery organ when
food is low and 0 when food is high.

Mutation test: 2000 sequential mutations produced genomes up to 766 genes,
242 neuron genes, and 106 organ copies; all 2000 mutated genomes compiled after
transactional acceptance (21966-byte codec payload).

## Status

Implemented and wired into the playable demo. `CreatureGenome` owns a
`GenomeV4`; the legacy `BodyGenome` / `NeuralGenome` implementations have been
removed, and the demo save schema is version 7.

Regulatory conditions are a compiler/tooling capability: the playable runtime
compiles with a neutral expression context, so regulation does not yet react to
live food, predator, or energy changes. The standalone compiler tests and the
mock benchmark exercise non-neutral contexts.
