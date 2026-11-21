using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4SensorRuntimeTests
{
    [Fact]
    public void Ports_AreDerivedFromExpressedOrgans()
    {
        GenomeV4? carrionGenome = null;
        for (var seed = 0; seed < 100 && carrionGenome is null; seed++)
        {
            var candidate = GenomeV4Factory.CreateDemoFounder(new Random(seed));
            var phenotype = GenomeV4Compiler.Compile(candidate, ExpressionContextV4.Neutral);
            if (phenotype.Body.Organs.Any(static organ => organ.Kind == OrganKindV4.CarrionSense))
                carrionGenome = candidate;
        }

        Assert.NotNull(carrionGenome);
        var compiled = GenomeV4Compiler.Compile(carrionGenome, ExpressionContextV4.Neutral);
        Assert.Contains((ushort)SensorKindV4.CorpseAhead, compiled.SensorPorts);
        Assert.Contains((ushort)ActionKindV4.Move, compiled.ActionPorts);
    }

    [Fact]
    public void CarrionPort_EvaluatesCorpseProximity()
    {
        GenomeV4? genome = null;
        PhenotypeV4? phenotype = null;
        for (var seed = 0; seed < 100 && genome is null; seed++)
        {
            var candidate = GenomeV4Factory.CreateDemoFounder(new Random(seed));
            var compiled = GenomeV4Compiler.Compile(candidate, ExpressionContextV4.Neutral);
            if (compiled.SensorPorts.Contains((ushort)SensorKindV4.CorpseAhead))
            {
                genome = candidate;
                phenotype = compiled;
            }
        }

        Assert.NotNull(phenotype);
        var sensors = new float[64];
        DemoSensorRuntime.ComputeInto(
            phenotype,
            new DemoSensorContext
            {
                Position = Vector2.Zero,
                Heading = 0f,
                Energy = 50f,
                MaxEnergy = 100f,
                SenseRange = 200f,
                Food = [],
                Creatures = [],
                Corpses = [new(0, new Vector2(80f, 0f))],
                SelfId = 0,
                OscillatorPhase = 0f,
                ArenaRadius = 10_000f,
            },
            sensors);
        Assert.True(sensors[(int)SensorKindV4.CorpseAhead] > 0f);
    }
    [Fact]
    public void FoodConeBatch_EvaluatesForwardFood()
    {
        PhenotypeV4? phenotype = null;
        for (var seed = 0; seed < 100 && phenotype is null; seed++)
        {
            var candidate = GenomeV4Factory.CreateDemoFounder(new Random(seed));
            var compiled = GenomeV4Compiler.Compile(candidate, ExpressionContextV4.Neutral);
            if (compiled.SensorPorts.Contains((ushort)SensorKindV4.FoodAhead))
                phenotype = compiled;
        }

        Assert.NotNull(phenotype);
        var sensors = new float[64];
        DemoSensorRuntime.ComputeInto(
            phenotype,
            new DemoSensorContext
            {
                Position = Vector2.Zero,
                Heading = 0f,
                Energy = 50f,
                MaxEnergy = 100f,
                SenseRange = 200f,
                Food = [new(0, new Vector2(80f, 0f))],
                Creatures = [],
                Corpses = [],
                SelfId = 0,
                OscillatorPhase = 0f,
                ArenaRadius = 10_000f,
            },
            sensors);
        Assert.True(sensors[(int)SensorKindV4.FoodAhead] > 0f);
    }

    [Fact]
    public void FoodTypeVision_ExposesTypeSpecificProximity()
    {
        var phenotype = CompileFoodTypeVisionPhenotype();
        var sensors = new float[64];
        DemoSensorRuntime.ComputeInto(
            phenotype,
            new DemoSensorContext
            {
                Position = Vector2.Zero,
                Heading = 0f,
                Energy = 50f,
                MaxEnergy = 100f,
                SenseRange = 200f,
                Food = [new(1, new Vector2(10f, 0f)), new(0, new Vector2(150f, 0f))],
                Creatures = [],
                Corpses = [],
                SelfId = 0,
                OscillatorPhase = 0f,
                ArenaRadius = 10_000f,
                FoodTypes = [FoodType.Soft, FoodType.Tough],
            },
            sensors);
        Assert.Equal(0.25f, sensors[(int)SensorKindV4.FoodSoft], 5);
        Assert.Equal(0.95f, sensors[(int)SensorKindV4.FoodTough], 5);
        Assert.Equal(0f, sensors[(int)SensorKindV4.FoodToxic]);
    }

    [Fact]
    public void FoodTypeVision_RejectsIdOutsideTypeTable()
    {
        var phenotype = CompileFoodTypeVisionPhenotype();
        var sensors = new float[64];
        var exception = Assert.Throws<InvalidOperationException>(() => DemoSensorRuntime.ComputeInto(
            phenotype,
            new DemoSensorContext
            {
                Position = Vector2.Zero,
                Heading = 0f,
                Energy = 50f,
                MaxEnergy = 100f,
                SenseRange = 200f,
                Food = [new(3, new Vector2(10f, 0f))],
                Creatures = [],
                Corpses = [],
                SelfId = 0,
                OscillatorPhase = 0f,
                ArenaRadius = 10_000f,
                FoodTypes = [FoodType.Soft],
            },
            sensors));
        Assert.Contains("FoodTypes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FoodTypeVision_OrganAbsentExposesNoTypeSensors()
    {
        var genome = new GenomeV4(
        [
            new GeneV4(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
        ], 2);
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        Assert.DoesNotContain((ushort)SensorKindV4.FoodSoft, phenotype.SensorPorts);
        Assert.DoesNotContain((ushort)SensorKindV4.FoodTough, phenotype.SensorPorts);
        Assert.DoesNotContain((ushort)SensorKindV4.FoodToxic, phenotype.SensorPorts);
    }

    private static PhenotypeV4 CompileFoodTypeVisionPhenotype()
    {
        var genome = new GenomeV4(
        [
            new GeneV4(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 0.6f)),
            new GeneV4(2, GeneKind.Organ, 1, 1f,
                GenePayloadV4.Organ(OrganKindV4.FoodTypeVision, 1f)),
        ], 3);
        return GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
    }

    [Fact]
    public void FoodValueVision_UsesGutDependentEnergy()
    {
        var phenotype = CompileFoodTypeVisionPhenotype();
        Assert.Contains((ushort)SensorKindV4.FoodValueAhead, phenotype.SensorPorts);
        var sensors = new float[64];
        var context = new DemoSensorContext
        {
            Position = Vector2.Zero,
            Heading = 0f,
            Energy = 50f,
            MaxEnergy = 100f,
            SenseRange = 200f,
            Food = [new(0, new Vector2(50f, 0f)), new(1, new Vector2(50f, 0f))],
            Creatures = [],
            Corpses = [],
            SelfId = 0,
            OscillatorPhase = 0f,
            ArenaRadius = 10_000f,
            FoodTypes = [FoodType.Soft, FoodType.Tough],
            HardDigest = true,
        };
        DemoSensorRuntime.ComputeInto(phenotype, context, sensors);
        var hardValue = sensors[(int)SensorKindV4.FoodValueAhead];
        Assert.Equal(25f / 32f * 0.75f, hardValue, 5);
        context = context with { HardDigest = false };
        var generalist = new float[64];
        DemoSensorRuntime.ComputeInto(phenotype, context, generalist);
        Assert.Equal(18f / 32f * 0.75f, generalist[(int)SensorKindV4.FoodValueAhead], 5);
        Assert.True(hardValue > generalist[(int)SensorKindV4.FoodValueAhead]);
    }

}
