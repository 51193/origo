using System;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4DemoAdapterTests
{
    [Fact]
    public void DemoFounder_AdaptsToBodyPhenotype()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var genome = GenomeV4Factory.CreateDemoFounder(new Random(seed));
            var body = DemoPhenotypeAdapter.ToBodyPhenotype(genome);
            Assert.True(body.SizeScale > 0f);
            Assert.True(float.IsFinite(body.MoveSpeed));
            Assert.True(body.SenseRange > 0f);
            Assert.True(body.VisionOrgan);
            Assert.True(float.IsFinite(body.MaxAge));
        }
    }

    [Fact]
    public void DemoFounders_CanExpressArmor()
    {
        var found = false;
        for (var seed = 0; seed < 100 && !found; seed++)
        {
            var body = DemoPhenotypeAdapter.ToBodyPhenotype(GenomeV4Factory.CreateDemoFounder(new Random(seed)));
            found = body.ArmorOrgan;
        }

        Assert.True(found, "Armor must be a reachable founder organ under the default content gate.");
    }

    [Fact]
    public void Armor_ReducesMoveAndTurnSpeed()
    {
        var genes = new System.Collections.Generic.List<GeneV4>
        {
            new(1, GeneKind.BodySegment, 1, 1f, GenePayloadV4.BodySegment(1f, 1f)),
            new(2, GeneKind.LifeHistory, 1, 1f, GenePayloadV4.LifeHistory(LifeTraitKindV4.Speed, 80f)),
            new(3, GeneKind.LifeHistory, 1, 1f, GenePayloadV4.LifeHistory(LifeTraitKindV4.TurnSpeed, 4f)),
            new(4, GeneKind.Organ, 1, 1f, GenePayloadV4.Organ(OrganKindV4.Armor, 0.8f)),
        };
        var armored = DemoPhenotypeAdapter.ToBodyPhenotype(
            new GenomeV4(genes, 5));
        var unarmored = DemoPhenotypeAdapter.ToBodyPhenotype(
            new GenomeV4(genes.GetRange(0, 3), 5));

        Assert.True(armored.MoveSpeed < unarmored.MoveSpeed);
        Assert.True(armored.TurnSpeed < unarmored.TurnSpeed);
        Assert.Equal(unarmored.MoveSpeed * Tuning.ArmorSpeedMultiplier, armored.MoveSpeed, 4);
        Assert.Equal(unarmored.TurnSpeed * Tuning.ArmorSpeedMultiplier, armored.TurnSpeed, 4);
    }

    [Fact]
    public void Adapter_RoundTripsThroughCodec()
    {
        var genome = GenomeV4Factory.CreateDemoFounder(new Random(42));
        var restored = GenomeV4Codec.Deserialize(GenomeV4Codec.Serialize(genome));
        var a = DemoPhenotypeAdapter.ToBodyPhenotype(genome);
        var b = DemoPhenotypeAdapter.ToBodyPhenotype(restored);
        Assert.Equal(a, b);
    }
}
