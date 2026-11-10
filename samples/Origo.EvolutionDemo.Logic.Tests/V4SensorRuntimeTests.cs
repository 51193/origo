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
}
