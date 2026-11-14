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
}
