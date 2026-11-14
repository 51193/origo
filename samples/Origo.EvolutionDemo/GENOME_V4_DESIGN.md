# Genome v4 design baseline

The runtime genome is the V4 graph implementation in
[`Origo.EvolutionDemo.Logic/V4`](../Origo.EvolutionDemo.Logic/V4/README.md).

## Summary

- Stable-ID genes with typed payload codecs.
- Variable-length BodyPlan organ instances, no fixed organ-slot arrays.
- Variable NeuralGraph nodes and synapses, no fixed latent matrix.
- Regulatory conditions evaluate at phenotype compile time.
- Energy budget covers body tissue, neural tissue, and genome complexity.
- Canonical binary codec with strict validation and hex display.
- Transactional mutation keeps offspring compilable.

## Runtime integration

`CreatureGenome` owns a `GenomeV4`. The demo compiles it to its current
`BodyPhenotype` and to a `CompiledBrainV4` adapter for sensor/action evaluation.
The old `BodyGenome`, `NeuralGenome`, and legacy tests are removed.

## Benchmark

Mock capacity, non-linear decision, energy trade-off, regulation, mutation, and
codec results are recorded in the V4 README. The 1h V4 ecosystem benchmark is
run with:

```bash
bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 5 \
  --fast-forward 3600 --save-slot v4_1h
```
