using System;
using System.Collections.Generic;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>
///     Maps a compiled V4 phenotype onto the legacy demo's presentation-facing
///     BodyPhenotype and BrainOutput while the runtime is migrated. This is a
///     temporary adapter, not a fixed genome representation.
/// </summary>
public static class DemoPhenotypeAdapter
{
    /// <summary>Compiles a V4 genome and adapts it to the demo's current phenotype.</summary>
    public static BodyPhenotype ToBodyPhenotype(GenomeV4 genome)
    {
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        return ToBodyPhenotype(phenotype);
    }

    /// <summary>Adapts a compiled V4 body to the demo's BodyPhenotype.</summary>
    public static BodyPhenotype ToBodyPhenotype(PhenotypeV4 phenotype)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        var organs = phenotype.Body.Organs;
        var size = TraitOrDefault(phenotype, LifeTraitKindV4.Size, 1f);
        var bodyRadius = Tuning.BodyRadiusBase * size;
        var spikeBattery = organs.Where(static organ => organ.Kind == OrganKindV4.SpikeBattery).ToList();
        var spikeCount = Math.Clamp(spikeBattery.Count, 1, 8);
        var pattern = Math.Abs(BitConverter.ToInt32(
            System.Security.Cryptography.SHA256.HashData(GenomeV4Codec.Serialize(phenotype.Genome)), 0)) % 12;
        return new BodyPhenotype(
            size,
            bodyRadius,
            bodyRadius + Tuning.FoodPickupPadding,
            Tuning.BodyMaxEnergyBase + (Tuning.BodyMaxEnergyPerSize * size * size),
            TraitOrDefault(phenotype, LifeTraitKindV4.Speed, Tuning.CreatureSpeed),
            TraitOrDefault(phenotype, LifeTraitKindV4.TurnSpeed, Tuning.TurnSpeed),
            TraitOrDefault(phenotype, LifeTraitKindV4.SenseRange, Tuning.SenseRange),
            TraitOrDefault(phenotype, LifeTraitKindV4.SpikeRange, Tuning.SpikeRadius),
            TraitOrDefault(phenotype, LifeTraitKindV4.Metabolism, 1f),
            TraitOrDefault(phenotype, LifeTraitKindV4.MaxAge, Tuning.MaxAgeSeconds),
            organs.Any(static organ => organ.Kind == OrganKindV4.SpikeOrgan),
            organs.Any(static organ => organ.Kind == OrganKindV4.Rhythm),
            organs.Any(static organ => organ.Kind == OrganKindV4.Vision),
            organs.Any(static organ => organ.Kind == OrganKindV4.CreatureSense),
            0,
            0,
            0,
            0,
            pattern,
            spikeBattery.Count > 0,
            spikeCount,
            MathF.PI / 4f,
            0f,
            organs.Any(static organ => organ.Kind == OrganKindV4.HardDigest),
            organs.Any(static organ => organ.Kind == OrganKindV4.ToxinResistance));
    }

    /// <summary>Maps the legacy 12-slot sensor buffer into V4 sensor interface values.</summary>
    public static float[] ToSensorValues(BodyPhenotype body, ReadOnlySpan<float> legacySensors)
    {
        var values = new float[16];
        if (body.VisionOrgan)
        {
            values[(int)SensorKindV4.FoodAhead] = legacySensors[(int)SensorId.FoodAhead];
            values[(int)SensorKindV4.FoodLeft] = legacySensors[(int)SensorId.FoodLeft];
            values[(int)SensorKindV4.FoodRight] = legacySensors[(int)SensorId.FoodRight];
        }

        if (body.CreatureSenseOrgan)
            values[(int)SensorKindV4.CreatureAhead] = legacySensors[(int)SensorId.CreatureAhead];
        values[(int)SensorKindV4.WallAhead] = legacySensors[(int)SensorId.WallAhead];
        values[(int)SensorKindV4.Energy] = legacySensors[(int)SensorId.Energy];
        if (body.RhythmOrgan)
        {
            values[(int)SensorKindV4.Sin] = legacySensors[(int)SensorId.Sin];
            values[(int)SensorKindV4.Cos] = legacySensors[(int)SensorId.Cos];
        }

        return values;
    }

    /// <summary>Adapts V4 action outputs to the demo's legacy BrainOutput.</summary>
    public static BrainOutput ToBrainOutput(BodyPhenotype body, PhenotypeV4 phenotype, ReadOnlySpan<float> legacySensors)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        return ToBrainOutput(body, GenomeV4Compiler.CompileBrain(phenotype), legacySensors);
    }

    /// <summary>Evaluates a precompiled graph without rebuilding it per tick.</summary>
    public static BrainOutput ToBrainOutput(
        BodyPhenotype body,
        CompiledBrainV4 brain,
        ReadOnlySpan<float> legacySensors)
    {
        ArgumentNullException.ThrowIfNull(brain);
        var sensors = ToSensorValues(body, legacySensors);
        var actions = new float[16];
        brain.Evaluate(sensors, actions);
        var spike = body.SpikeOrgan ? Math.Clamp(actions[(int)ActionKindV4.Spike], -1f, 1f) : 0f;
        var aim = body.SpikeOrgan ? Math.Clamp(actions[(int)ActionKindV4.Aim], -1f, 1f) : 0f;
        return new BrainOutput(
            Math.Clamp(actions[(int)ActionKindV4.Move], -1f, 1f),
            Math.Clamp(actions[(int)ActionKindV4.Turn], -1f, 1f),
            spike,
            aim);
    }

    private static float TraitOrDefault(PhenotypeV4 phenotype, LifeTraitKindV4 trait, float fallback) =>
        phenotype.Body.LifeTraits.TryGetValue(trait, out var value) ? value : fallback;
}
