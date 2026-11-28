using System;
using System.Linq;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GameplayGenomeGateTests
{
    [Fact]
    public void LockedSpikeOrgan_IsNotAddedToFounder()
    {
        var gate = new GenomeContentGate(
            organ => organ != OrganKindV4.SpikeOrgan,
            static _ => true,
            static _ => true);
        for (var seed = 0; seed < 50; seed++)
        {
            var genome = GenomeV4Factory.CreateDemoFounder(new Random(seed), gate);
            var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
            Assert.DoesNotContain(phenotype.Body.Organs,
                static organ => organ.Kind == OrganKindV4.SpikeOrgan);
        }
    }

    [Fact]
    public void UnlockedArmor_IsAddedByMutation()
    {
        var gate = new GenomeContentGate(
            organ => organ == OrganKindV4.Armor,
            static _ => true,
            static _ => true);
        var random = new Random(7);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        var found = false;
        for (var i = 0; i < 500 && !found; i++)
        {
            genome = GenomeV4Mutator.Mutate(genome, random, gate: gate);
            var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
            found = phenotype.Body.Organs.Any(static organ => organ.Kind == OrganKindV4.Armor);
        }

        Assert.True(found, "Armor must be reachable through the mutation operator set.");
    }

    [Fact]
    public void LockedActions_AreNotAddedByMutation()
    {
        var gate = new GenomeContentGate(
            static _ => true,
            action => action is ActionKindV4.Move or ActionKindV4.Turn,
            static _ => true);
        var random = new Random(42);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        for (var i = 0; i < 100; i++)
            genome = GenomeV4Mutator.Mutate(genome, random, gate: gate);
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        Assert.DoesNotContain((ushort)ActionKindV4.Spike, phenotype.ActionPorts);
        Assert.DoesNotContain((ushort)ActionKindV4.Sprint, phenotype.ActionPorts);
    }
    [Fact]
    public void MutationCanCreateFoodTypeVisionWhenUnlocked()
    {
        var gate = new GenomeContentGate(
            organ => organ == OrganKindV4.FoodTypeVision,
            static _ => true,
            static _ => true);
        var random = new Random(20261121);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        var found = false;
        for (var i = 0; i < 2000 && !found; i++)
        {
            genome = GenomeV4Mutator.Mutate(genome, random, gate: gate);
            var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
            found = phenotype.Body.Organs.Any(static organ => organ.Kind == OrganKindV4.FoodTypeVision);
        }

        Assert.True(found, "FoodTypeVision was not reachable through mutation.");
    }

    [Fact]
    public void MutationCannotExposeLockedSensorThroughAnUnlockedOrgan()
    {
        var gate = new GenomeContentGate(
            organ => organ == OrganKindV4.CarrionSense,
            static _ => true,
            sensor => sensor != SensorKindV4.CorpseAhead);
        var random = new Random(20261122);
        var genome = GenomeV4Factory.CreateSimpleReflex();
        for (var i = 0; i < 2000; i++)
        {
            genome = GenomeV4Mutator.Mutate(genome, random, gate: gate);
            var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
            Assert.DoesNotContain((ushort)SensorKindV4.CorpseAhead, phenotype.SensorPorts);
        }
    }

}
