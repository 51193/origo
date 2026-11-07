using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class BodyGenomeTests
{
    [Fact]
    public void DefaultTemplate_ExpressesCalibratedBody()
    {
        Assert.Equal(72, BodyGenome.DefaultTemplate.Length);
        var body = BodyGenome.Decode(BodyGenome.DefaultTemplate);

        Assert.InRange(body.SizeScale, 0.99f, 1.01f);
        Assert.InRange(body.BodyRadius, 4.95f, 5.05f);
        Assert.InRange(body.FoodPickupRadius, 13.95f, 14.05f);
        Assert.InRange(body.MaxEnergy, 198f, 202f);
        Assert.InRange(body.MoveSpeed, 54f, 56f);
        Assert.InRange(body.TurnSpeed, 2.75f, 2.85f);
        Assert.InRange(body.SenseRange, 119f, 122f);
        Assert.InRange(body.SpikeRadius, 47f, 49f);
        Assert.Equal(2, body.HiddenLayers);
        Assert.Equal(5, body.HiddenWidth1);
        Assert.Equal(5, body.HiddenWidth2);
        Assert.True(body.SpikeOrgan);
        Assert.True(body.RhythmOrgan);
        Assert.True(body.VisionOrgan);
        Assert.True(body.CreatureSenseOrgan);
        Assert.Equal(8, body.ActiveSensorCount);
        Assert.Equal(3, body.ActiveOutputCount);
    }

    [Fact]
    public void UnknownCodonsAreNeutralAndUseDefaults()
    {
        var body = BodyGenome.Decode(new string('F', 72));

        Assert.InRange(body.SizeScale, 0.99f, 1.01f);
        Assert.False(body.SpikeOrgan);
        Assert.False(body.VisionOrgan);
        Assert.Equal(2, body.ActiveSensorCount);
        Assert.Equal(2, body.ActiveOutputCount);
        Assert.Equal(2, body.HiddenLayers);
    }

    [Fact]
    public void NumericTraits_UseMedianOfValidCodons()
    {
        var chars = BodyGenome.DefaultTemplate.ToCharArray();
        ReplaceCodon(chars, 0, "100");
        ReplaceCodon(chars, 1, "1BF");
        ReplaceCodon(chars, 2, "13A");

        var body = BodyGenome.Decode(new string(chars));

        Assert.InRange(body.SizeScale, 0.99f, 1.01f);
    }

    [Fact]
    public void OrganCodons_AreOrCombinedAndSparse()
    {
        var chars = new char[72];
        Array.Fill(chars, 'F');
        ReplaceCodon(chars, 0, "001");
        ReplaceCodon(chars, 1, "00F");
        ReplaceCodon(chars, 2, "010");

        var body = BodyGenome.Decode(new string(chars));

        Assert.True(body.SpikeOrgan);
        Assert.True(body.RhythmOrgan);
        Assert.False(body.VisionOrgan);
        Assert.Equal(4, body.ActiveSensorCount);
    }

    [Fact]
    public void Mutate_StepIsNibbleBoundedAndKeepsStructure()
    {
        var child = BodyGenome.Mutate(
            BodyGenome.DefaultTemplate,
            1d,
            0d,
            0d,
            new Random(4),
            out var changes);

        Assert.True(BodyGenome.IsStructurallyValid(child));
        Assert.Equal(BodyGenome.DefaultTemplate.Length, child.Length);
        Assert.True(changes > 0);
        Assert.NotEqual(BodyGenome.DefaultTemplate, child);
    }

    [Fact]
    public void Mutate_LengthChangeUsesWholeCodons()
    {
        var parent = BodyGenome.DefaultTemplate;
        var child = BodyGenome.Mutate(parent, 0d, 1d, 0d, new Random(5), out var changes);

        Assert.True(BodyGenome.IsStructurallyValid(child));
        Assert.Equal(1, changes);
        Assert.True(child.Length == parent.Length + 3 || child.Length == parent.Length - 3);
    }

    [Fact]
    public void Mutate_FrameshiftChangesLengthByOneHex()
    {
        var parent = BodyGenome.DefaultTemplate;
        var child = BodyGenome.Mutate(parent, 0d, 0d, 1d, new Random(6), out var changes);

        Assert.True(BodyGenome.IsStructurallyValid(child));
        Assert.Equal(1, changes);
        Assert.True(child.Length == parent.Length + 1 || child.Length == parent.Length - 1);
    }

    [Fact]
    public void Decode_RejectsMalformedBodies()
    {
        Assert.Throws<ArgumentException>(() => BodyGenome.Decode("ABC"));
        Assert.Throws<ArgumentException>(() => BodyGenome.Decode(new string('G', 72)));
    }

    [Fact]
    public void BodySensorMask_LeavesInactiveLatentSlotsZero()
    {
        var body = BodyGenome.Decode(BodyGenome.DefaultTemplate);
        var sensors = new float[NeuralGenome.SensorSlots];
        SensorMath.ComputeInto(
            body,
            new System.Numerics.Vector2(0f, 0f),
            0f,
            100f,
            [],
            [],
            -1,
            0.25f,
            10_000f,
            sensors);

        Assert.True(sensors[(int)SensorId.FoodAhead] >= 0f);
        Assert.True(sensors[(int)SensorId.WallAhead] >= 0f);
        Assert.Equal(0f, sensors[(int)SensorId.FutureA]);
        Assert.Equal(0f, sensors[(int)SensorId.FutureD]);
    }

    private static void ReplaceCodon(char[] chars, int codonIndex, string codon)
    {
        for (var i = 0; i < 3; i++)
            chars[(codonIndex * 3) + i] = codon[i];
    }
}
