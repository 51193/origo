using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4RobustnessTests
{
    [Fact]
    public void Deserialize_RejectsOversizedGeneCountBeforeAllocating()
    {
        var data = GenomeV4Codec.Serialize(GenomeV4Factory.CreateSimpleReflex());
        BitConverter.GetBytes(int.MaxValue).CopyTo(data, 4);

        Assert.Throws<InvalidDataException>(() => GenomeV4Codec.Deserialize(data));
    }

    [Fact]
    public void Deserialize_RejectsTruncatedPayload()
    {
        var data = GenomeV4Codec.Serialize(GenomeV4Factory.CreateSimpleReflex());
        Assert.Throws<InvalidDataException>(() => GenomeV4Codec.Deserialize(data[..^4]));
    }

    [Fact]
    public void Deserialize_RejectsTrailingBytes()
    {
        var data = GenomeV4Codec.Serialize(GenomeV4Factory.CreateSimpleReflex());
        Assert.Throws<InvalidDataException>(() =>
            GenomeV4Codec.Deserialize([.. data, 0x00]));
    }

    [Fact]
    public void TryDecode_TruncatedBase64ReturnsFalseWithoutThrowing()
    {
        var encoded = GenomeV4Codec.ToBase64(GenomeV4Factory.CreateSimpleReflex());

        Assert.False(CreatureGenome.TryDecode(encoded[..^8], out _));
    }

    [Fact]
    public void Validate_RejectsShortPayloads()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, GeneKind.Organ, 1, 1f, [1, 0]),
        };
        Assert.Throws<InvalidDataException>(() => GenomeV4Validator.Validate(new GenomeV4(genes, 3)));
    }

    [Fact]
    public void Validate_RejectsUnknownGeneKind()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, (GeneKind)99, 1, 1f, [1]),
        };
        Assert.Throws<InvalidDataException>(() => GenomeV4Validator.Validate(new GenomeV4(genes, 3)));
    }

    [Fact]
    public void Validate_RejectsUnknownReflexInterface()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, GeneKind.Reflex, 1, 1f,
                GenePayloadV4.Reflex((SensorKindV4)999, ActionKindV4.Move, 1f)),
        };
        Assert.Throws<InvalidDataException>(() => GenomeV4Validator.Validate(new GenomeV4(genes, 3)));
    }

    [Theory]
    [InlineData(200f)]
    [InlineData(-2f)]
    [InlineData(float.NaN)]
    public void Validate_RejectsOutOfRangeLifeTraits(float size)
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, GeneKind.LifeHistory, 1, 1f, GenePayloadV4.LifeHistory(LifeTraitKindV4.Size, size)),
        };
        Assert.Throws<InvalidDataException>(() => GenomeV4Validator.Validate(new GenomeV4(genes, 3)));
    }

    [Fact]
    public void CreateDemoFounder_UsesConfiguredLifeTraitRanges()
    {
        var previous = DemoSettings.Current;
        try
        {
            var settings = DemoSettings.Defaults();
            settings.BodySizeMin = 0.9f;
            settings.BodySizeMax = 1.0f;
            settings.BodySpeedMin = 50f;
            settings.BodySpeedMax = 60f;
            settings.BodyTurnMin = 2f;
            settings.BodyTurnMax = 3f;
            settings.BodySenseMin = 100f;
            settings.BodySenseMax = 120f;
            settings.BodySpikeMin = 40f;
            settings.BodySpikeMax = 50f;
            settings.BodyMetabolismMin = 0.6f;
            settings.BodyMetabolismMax = 0.7f;
            settings.BodyAgeMin = 150f;
            settings.BodyAgeMax = 200f;
            DemoSettings.Current = settings;

            for (var seed = 0; seed < 20; seed++)
            {
                var founder = CreatureGenome.RandomFounder(new Random(seed));
                var traits = GenomeV4Compiler.Compile(
                    founder.Genome, ExpressionContextV4.Neutral).Body.LifeTraits;
                Assert.InRange(traits[LifeTraitKindV4.Size], 0.9f, 1f);
                Assert.InRange(traits[LifeTraitKindV4.Speed], 50f, 60f);
                Assert.InRange(traits[LifeTraitKindV4.TurnSpeed], 2f, 3f);
                Assert.InRange(traits[LifeTraitKindV4.SenseRange], 100f, 120f);
                Assert.InRange(traits[LifeTraitKindV4.SpikeRange], 40f, 50f);
                Assert.InRange(traits[LifeTraitKindV4.Metabolism], 0.6f, 0.7f);
                Assert.InRange(traits[LifeTraitKindV4.MaxAge], 150f, 200f);
            }
        }
        finally
        {
            DemoSettings.Current = previous;
        }
    }

    [Fact]
    public void Mutate_ClampsLifeTraitsToConfiguredRanges()
    {
        var previous = DemoSettings.Current;
        try
        {
            var settings = DemoSettings.Defaults();
            settings.BodySizeMin = 0.8f;
            settings.BodySizeMax = 1.1f;
            settings.BodySpeedMin = 40f;
            settings.BodySpeedMax = 70f;
            settings.BodyTurnMin = 1.5f;
            settings.BodyTurnMax = 3f;
            settings.BodySenseMin = 80f;
            settings.BodySenseMax = 150f;
            settings.BodySpikeMin = 20f;
            settings.BodySpikeMax = 80f;
            settings.BodyMetabolismMin = 0.55f;
            settings.BodyMetabolismMax = 1f;
            settings.BodyAgeMin = 150f;
            settings.BodyAgeMax = 400f;
            DemoSettings.Current = settings;
            var random = new Random(2026);
            var genome = CreatureGenome.RandomFounder(random);
            var profile = MutationProfile.FromSettings(settings).WithSteps(3);
            for (var step = 0; step < 300; step++)
            {
                genome = CreatureGenome.Mutate(genome, profile, random, out _);
                var body = genome.Phenotype;
                Assert.InRange(body.SizeScale, 0.8f, 1.1f);
                Assert.InRange(body.MoveSpeed, 40f, 70f);
                Assert.InRange(body.TurnSpeed, 1.5f, 3f);
                Assert.InRange(body.SenseRange, 80f, 150f);
                Assert.InRange(body.SpikeRadius, 20f, 80f);
                Assert.InRange(body.BasalMetabolism, 0.55f, 1f);
                Assert.InRange(body.MaxAge, 150f, 400f);
            }
        }
        finally
        {
            DemoSettings.Current = previous;
        }
    }

    [Fact]
    public void Compile_LatentSensorGeneWithoutOrganCreatesNoDeadNode()
    {
        var genes = new List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, GeneKind.Neuron, 1, 1f,
                GenePayloadV4.Neuron(NeuronKindV4.Sensor, (ushort)SensorKindV4.FoodAhead,
                    ActivationKindV4.Linear, 0f)),
        };
        var phenotype = GenomeV4Compiler.Compile(new GenomeV4(genes, 3), ExpressionContextV4.Neutral);

        Assert.DoesNotContain(phenotype.Brain.Nodes,
            node => node.Kind == NeuronKindV4.Sensor && node.InterfaceId == (ushort)SensorKindV4.FoodAhead);
        Assert.DoesNotContain((ushort)SensorKindV4.FoodAhead, phenotype.SensorPorts);
    }

    [Fact]
    public void DemoFounderBrain_ContainsOnlyExpressedPorts()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var phenotype = GenomeV4Compiler.Compile(
                GenomeV4Factory.CreateDemoFounder(new Random(seed)), ExpressionContextV4.Neutral);
            var sensors = phenotype.SensorPorts.ToHashSet();
            var actions = phenotype.ActionPorts.ToHashSet();
            foreach (var node in phenotype.Brain.Nodes)
            {
                if (node.Kind == NeuronKindV4.Sensor)
                    Assert.Contains(node.InterfaceId, sensors);
                if (node.Kind == NeuronKindV4.Output)
                    Assert.Contains(node.InterfaceId, actions);
            }
        }
    }

    [Fact]
    public void WallAhead_UsesContextArenaRadius()
    {
        var phenotype = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateSimpleReflex(), ExpressionContextV4.Neutral);
        var sensors = new float[64];
        DemoSensorRuntime.ComputeInto(
            phenotype,
            new DemoSensorContext
            {
                Position = new Vector2(1_000f, 0f),
                Heading = 0f,
                Energy = 50f,
                MaxEnergy = 100f,
                SenseRange = 200f,
                Food = [],
                Creatures = [],
                Corpses = [],
                SelfId = 0,
                OscillatorPhase = 0f,
                ArenaRadius = 1_100f,
            },
            sensors);

        Assert.Equal(0.5f, sensors[(int)SensorKindV4.WallAhead], 5);
    }

    [Fact]
    public void SensorDisplay_FormatsExpressedPortsWithRegistryLabels()
    {
        var display = SensorDisplayV4.Format(
            [(ushort)SensorKindV4.FoodAhead, (ushort)SensorKindV4.WallAhead],
            [0f, 0.75f, 0f, 0f, 0.5f]);

        Assert.Contains("Food ahead 0.75", display, StringComparison.Ordinal);
        Assert.Contains("Wall ahead 0.50", display, StringComparison.Ordinal);
    }

    [Fact]
    public void FeedRequestMath_AddsConfiguredAmount()
    {
        Assert.Equal(42, FeedRequestMath.Add(2, 40));
        Assert.Equal(0, FeedRequestMath.Add(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FeedRequestMath.Add(-1, 40));
        Assert.Throws<ArgumentOutOfRangeException>(() => FeedRequestMath.Add(0, -1));
    }

    [Fact]
    public void Mutator_CanChangeReflexGain()
    {
        var genome = new GenomeV4(
        [
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, GeneKind.Organ, 1, 1f, GenePayloadV4.Organ(OrganKindV4.Vision, 0.5f)),
            new(3, GeneKind.Reflex, 1, 1f,
                GenePayloadV4.Reflex(SensorKindV4.FoodAhead, ActionKindV4.Move, 1f)),
        ], 4);
        var settings = new GenomeMutationSettingsV4
        {
            Expression = 0d,
            CopyCount = 0d,
            Duplicate = 0d,
            Delete = 0d,
            Payload = 1d,
            AddNeuron = 0d,
            AddSynapse = 0d,
            AddReflex = 0d,
            AddOrgan = 0d,
            AddRegulation = 0d,
            AddSegment = 0d,
        };
        var random = new Random(17);
        var changed = false;
        for (var step = 0; step < 200 && !changed; step++)
        {
            genome = GenomeV4Mutator.Mutate(genome, random, settings);
            var reflex = genome.Genes.Single(gene => gene.Kind == GeneKind.Reflex);
            changed = MathF.Abs(GenePayloadV4.ReadReflex(reflex.Payload).Gain - 1f) > 0.0001f;
        }

        Assert.True(changed, "Reflex gain must be a mutable payload field.");
    }
}
