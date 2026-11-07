# Genome v2 design: body codons and latent brain

This document records the agreed design baseline for the body/neural genome split.
It is a design document only; implementation lands in subsequent commits.

## Body genome

- Three hex characters per codon: opcode nibble plus argument byte.
- Variable length in codon units: minimum 8, base 24, maximum 128 codons.
- Structural parser reads from offset zero. Incomplete trailing hex characters are
  ignored; malformed brain data is rejected.
- Unknown opcodes and invalid argument ranges are neutral.
- Organ codons are OR-combined. Numeric traits use the median of all valid
  mappings, then clamp. Duplicate codons therefore provide redundancy rather than
  unbounded dosage.

### Opcode table

| Opcode | Trait | Valid argument | Mapping |
|---|---|---|---|
| 0x | Organs | 00-3F | 16 synonyms per organ |
| 1x | Size | 00-BF | 0.65-1.80 |
| 2x | Move speed | 00-9F | 30-90 |
| 3x | Turn speed | 00-9F | 1.5-4.5 |
| 4x | Sense range | 00-9F | 60-260 |
| 5x | Spike range | 00-7F | 20-120 |
| 6x | Metabolism | 00-7F | 0.55-1.60 |
| 7x | Max age | 00-BF | 120-600 |
| 8x | Hidden layers | 00-2F | 1-3 |
| 9x | Width 1 | 00-7F | 1-8 |
| Ax | Width 2 | 00-7F | 1-8 |
| Bx | Width 3 | 00-7F | 1-8 |
| Cx | Appearance | 00-BF | pattern 0-11 |
| Dx | Neural gain | 00-3F | reserved |
| Ex | Extension | none | neutral |
| Fx | Reserved | none | neutral |

The initial template is 24 codons and covers size, speed, turn, sense, spike,
metabolism, age, two active hidden layers, three widths, all four organs, an
appearance pattern, and neutral reserved codons.

### Phenotype

- `bodyRadius = 5 * sizeScale`
- `foodPickupRadius = bodyRadius + 9`
- `maxEnergy = 120 + 80 * sizeScale^2`
- Sensors: always wall and energy; vision adds three food directions;
  creature-sense adds one creature direction; rhythm adds sin/cos.
- Outputs: move and turn always; spike only with the spike organ.
- Brain active topology is selected by the body phenotype.

## Neural genome

- Fixed maximum latent lattice, exactly 372 hex digits.
- 12 sensor slots, 3 hidden layers of 8 nodes, 4 output slots.
- Body selects sensor/output masks, active hidden layer count, and active widths.
- Body changes only change active slices; they cannot make the neural genome
  illegal.
- Active layer count 1-3; the last active hidden layer feeds the output
  projection.
- Future sensors/actions use reserved slots. Exceeding the reserved structure is
  a format upgrade, not a body mutation.

## Mutation

- Body step mutation: random hex position, value +/-1 modulo 16, fully random
  position.
- Body length mutation: duplicate/delete a complete codon at codon boundaries.
- Frameshift mutation: optional, default disabled; arbitrary hex insertion or
  deletion shifts all downstream codons.
- Brain step mutation: random fixed-position hex value +/-1.
- No old-save migration; incompatible saves fail explicitly.

## Reproduction cost

The parent pays a weak sublinear length penalty:

```text
reproCost = BaseParentCost * (1 + 0.05 * ln(1 + bodyHexLength / 72))
```

This permits redundant genes and polygeny without allowing unbounded growth.
