using System;
using System.Linq;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

[CollectionDefinition("GenomeGateSerial", DisableParallelization = true)]
public sealed class GenomeGateSerial
{
}

[Collection("GenomeGateSerial")]
public sealed class GameplayGenomeGateTests
{
    [Fact]
    public void LockedSpikeOrgan_IsNotAddedToFounder()
    {
        var savedOrgan = GenomeContentGate.OrganFilter;
        try
        {
            GenomeContentGate.OrganFilter = organ => organ != OrganKindV4.SpikeOrgan;
            for (var seed = 0; seed < 50; seed++)
            {
                var genome = GenomeV4Factory.CreateDemoFounder(new Random(seed));
                var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
                Assert.DoesNotContain(phenotype.Body.Organs,
                    static organ => organ.Kind == OrganKindV4.SpikeOrgan);
            }
        }
        finally
        {
            GenomeContentGate.OrganFilter = savedOrgan;
        }
    }

    [Fact]
    public void UnlockedArmor_IsAddedByMutation()
    {
        var savedOrgan = GenomeContentGate.OrganFilter;
        try
        {
            GenomeContentGate.OrganFilter = organ => organ == OrganKindV4.Armor;
            var random = new Random(7);
            var genome = GenomeV4Factory.CreateSimpleReflex();
            var found = false;
            for (var i = 0; i < 500 && !found; i++)
            {
                genome = GenomeV4Mutator.Mutate(genome, random);
                var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
                found = phenotype.Body.Organs.Any(static organ => organ.Kind == OrganKindV4.Armor);
            }

            Assert.True(found, "Armor must be reachable through the mutation operator set.");
        }
        finally
        {
            GenomeContentGate.OrganFilter = savedOrgan;
        }
    }

    [Fact]
    public void LockedActions_AreNotAddedByMutation()
    {
        var saved = GenomeContentGate.ActionFilter;
        try
        {
            GenomeContentGate.ActionFilter = action => action is ActionKindV4.Move or ActionKindV4.Turn;
            var random = new Random(42);
            var genome = GenomeV4Factory.CreateSimpleReflex();
            for (var i = 0; i < 100; i++)
                genome = GenomeV4Mutator.Mutate(genome, random);
            var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
            Assert.DoesNotContain((ushort)ActionKindV4.Spike, phenotype.ActionPorts);
            Assert.DoesNotContain((ushort)ActionKindV4.Sprint, phenotype.ActionPorts);
        }
        finally
        {
            GenomeContentGate.ActionFilter = saved;
        }
    }
    [Fact]
    public void MutationCanCreateFoodTypeVisionWhenUnlocked()
    {
        var saved = GenomeContentGate.OrganFilter;
        try
        {
            GenomeContentGate.OrganFilter = organ => organ == OrganKindV4.FoodTypeVision;
            var random = new Random(20261121);
            var genome = GenomeV4Factory.CreateSimpleReflex();
            var found = false;
            for (var i = 0; i < 2000 && !found; i++)
            {
                genome = GenomeV4Mutator.Mutate(genome, random);
                var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
                found = phenotype.Body.Organs.Any(static organ => organ.Kind == OrganKindV4.FoodTypeVision);
            }

            Assert.True(found, "FoodTypeVision was not reachable through mutation.");
        }
        finally
        {
            GenomeContentGate.OrganFilter = saved;
        }
    }

    [Fact]
    public void MutationCannotExposeLockedSensorThroughAnUnlockedOrgan()
    {
        var savedOrgan = GenomeContentGate.OrganFilter;
        var savedSensor = GenomeContentGate.SensorFilter;
        try
        {
            GenomeContentGate.OrganFilter = organ => organ == OrganKindV4.CarrionSense;
            GenomeContentGate.SensorFilter = sensor => sensor != SensorKindV4.CorpseAhead;
            var random = new Random(20261122);
            var genome = GenomeV4Factory.CreateSimpleReflex();
            for (var i = 0; i < 2000; i++)
            {
                genome = GenomeV4Mutator.Mutate(genome, random);
                var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
                Assert.DoesNotContain((ushort)SensorKindV4.CorpseAhead, phenotype.SensorPorts);
            }
        }
        finally
        {
            GenomeContentGate.OrganFilter = savedOrgan;
            GenomeContentGate.SensorFilter = savedSensor;
        }
    }

}
