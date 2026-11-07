using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class NeuralGenomeTests
{
    [Fact]
    public void Layout_IsFixedThreeHundredSeventyTwoHexDigits()
    {
        Assert.Equal(372, NeuralGenome.Length);
        Assert.Equal(96, NeuralGenome.WeightsInputLength);
        Assert.Equal(64, NeuralGenome.WeightsHidden12Length);
        Assert.Equal(64, NeuralGenome.WeightsHidden23Length);
        Assert.Equal(96, NeuralGenome.WeightsHidden1OutLength * 3);
        Assert.Equal(24, NeuralGenome.Types3Offset + NeuralGenome.HiddenSlots - NeuralGenome.Types1Offset);
        Assert.Equal(4, NeuralGenome.OutputBiasOffset + NeuralGenome.OutputSlots - NeuralGenome.OutputBiasOffset);
    }

    [Fact]
    public void CalibratedFounder_IsValidAndFinite()
    {
        var genome = NeuralGenome.CalibratedFounderTemplate();
        var body = BodyGenome.Decode(BodyGenome.DefaultTemplate);
        var brain = NeuralGenome.Decode(genome);
        var sensors = new float[NeuralGenome.SensorSlots];
        sensors[(int)SensorId.FoodAhead] = 0.6f;
        sensors[(int)SensorId.WallAhead] = 0.2f;
        sensors[(int)SensorId.Sin] = 0.4f;

        var output = brain.Evaluate(body, sensors);

        Assert.True(NeuralGenome.IsValid(genome));
        Assert.True(float.IsFinite(output.MoveDrive));
        Assert.True(float.IsFinite(output.TurnDrive));
        Assert.True(float.IsFinite(output.SpikeDrive));
    }

    [Fact]
    public void BodyTopologyChanges_KeepNeuralGenomeUsable()
    {
        var brain = NeuralGenome.Decode(NeuralGenome.CalibratedFounderTemplate());
        var body = BodyGenome.Decode(BodyGenome.DefaultTemplate);
        var sensors = new float[NeuralGenome.SensorSlots];

        var oneLayer = body with { HiddenLayers = 1, HiddenWidth1 = 3 };
        var threeLayers = body with { HiddenLayers = 3, HiddenWidth1 = 8, HiddenWidth2 = 8, HiddenWidth3 = 8 };
        var noSpike = body with { SpikeOrgan = false, VisionOrgan = false, RhythmOrgan = false };

        var one = brain.Evaluate(oneLayer, sensors);
        var three = brain.Evaluate(threeLayers, sensors);
        var masked = brain.Evaluate(noSpike, sensors);

        Assert.True(float.IsFinite(one.MoveDrive));
        Assert.True(float.IsFinite(three.TurnDrive));
        Assert.Equal(0f, masked.SpikeDrive);
    }

    [Fact]
    public void Mutate_ZeroRateIsIdentity()
    {
        var parent = NeuralGenome.CalibratedFounderTemplate();
        var child = NeuralGenome.Mutate(parent, 0d, new Random(3), out var changes);

        Assert.Equal(parent, child);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Mutate_StepChangesNibblesWithoutChangingLength()
    {
        var parent = NeuralGenome.CalibratedFounderTemplate();
        var child = NeuralGenome.Mutate(parent, 0.25d, new Random(4), out var changes);

        Assert.True(NeuralGenome.IsValid(child));
        Assert.Equal(parent.Length, child.Length);
        Assert.True(changes > 0);
        Assert.NotEqual(parent, child);
    }

    [Fact]
    public void RandomAndBlankAreStructurallyValid()
    {
        Assert.True(NeuralGenome.IsValid(NeuralGenome.Random(new Random(1))));
        Assert.True(NeuralGenome.IsValid(NeuralGenome.CreateBlank()));
        Assert.False(NeuralGenome.IsValid(new string('0', 371)));
        Assert.False(NeuralGenome.IsValid(new string('G', 372)));
    }
}
