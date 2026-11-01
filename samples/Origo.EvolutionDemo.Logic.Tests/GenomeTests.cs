using System;
using System.Globalization;
using System.Linq;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public class GenomeTests
{
    [Fact]
    public void Layout_IsOneHundredThreeHexDigits()
    {
        Assert.Equal(103, Genome.Length);
        Assert.Equal(8 * 5, Genome.Weights0Length);
        Assert.Equal(5 * 5, Genome.Weights1Length);
        Assert.Equal(5 * 3, Genome.Weights2Length);
        Assert.Equal(3, Genome.Bias3Length);
        Assert.Equal(5, Genome.Type1Length);
        Assert.Equal(5, Genome.Type2Length);
    }

    [Fact]
    public void Random_IsValidAndDeterministicForSameSeed()
    {
        var first = Genome.Random(new Random(1234));
        var second = Genome.Random(new Random(1234));

        Assert.True(Genome.IsValid(first));
        Assert.Equal(first, second);
        Assert.Equal(Genome.Length, first.Length);
        Assert.Contains(first, static c => c != '0');
    }

    [Fact]
    public void Random_AlwaysHasAtLeastOneOutputEdge()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var genome = Genome.Random(new Random(seed));
            var outputs = genome.AsSpan(Genome.Weights2Offset, Genome.Weights2Length);
            Assert.True(outputs.IndexOfAnyExcept('0') >= 0);
        }
    }

    [Fact]
    public void CalibratedFounderTemplate_ForagesAndAvoidsWalls()
    {
        var brain = Genome.Decode(Genome.CalibratedFounderTemplate());
        var neutral = new float[Genome.SensorCount];
        var foodAhead = new float[Genome.SensorCount];
        foodAhead[0] = 0.7f;
        var foodRight = new float[Genome.SensorCount];
        foodRight[2] = 0.7f;
        var foodLeft = new float[Genome.SensorCount];
        foodLeft[1] = 0.7f;
        var wallAhead = new float[Genome.SensorCount];
        wallAhead[3] = 0.7f;

        var neutralOutput = brain.Evaluate(neutral);
        var aheadOutput = brain.Evaluate(foodAhead);
        var rightOutput = brain.Evaluate(foodRight);
        var leftOutput = brain.Evaluate(foodLeft);
        var wallOutput = brain.Evaluate(wallAhead);

        Assert.True(neutralOutput.MoveDrive > Tuning.DriveThreshold);
        Assert.True(aheadOutput.MoveDrive > neutralOutput.MoveDrive);
        Assert.True(rightOutput.TurnDrive > 0.1f);
        Assert.True(leftOutput.TurnDrive < -0.1f);
        Assert.True(wallOutput.TurnDrive > 0.1f);
        Assert.True(neutralOutput.TurnDrive is > -0.9f and < 0.9f);
        Assert.True(neutralOutput.SpikeDrive < Tuning.SpikeThreshold);
    }

    [Fact]
    public void RandomFounder_MutatesTemplateButKeepsForwardDrive()
    {
        var random = new Random(77);
        var first = Genome.RandomFounder(random);
        var second = Genome.RandomFounder(random);

        Assert.True(Genome.IsValid(first));
        Assert.NotEqual(first, second);
        Assert.Equal('F', first[Genome.Bias3Offset]);
        var output = Genome.Decode(first).Evaluate(new float[Genome.SensorCount]);
        Assert.True(output.MoveDrive > Tuning.DriveThreshold);
    }

    [Fact]
    public void RandomFounder_StartsWithForwardDriveWithoutEvolvedEdges()
    {
        var brain = Genome.Decode(Genome.RandomFounder(new Random(99)));
        var zeroSensors = new float[Genome.SensorCount];
        var output = brain.Evaluate(zeroSensors);

        Assert.True(output.MoveDrive > Tuning.DriveThreshold);
    }

    [Fact]
    public void HiddenNodeTypeGenes_ChangeActivationBehavior()
    {
        var sigmoid = Genome.Decode(BuildTypeProbeGenome(Genome.NodeTypeSigmoid))
            .Evaluate(new float[Genome.SensorCount]);
        var linear = Genome.Decode(BuildTypeProbeGenome(Genome.NodeTypeLinear))
            .Evaluate(new float[Genome.SensorCount]);
        var invert = Genome.Decode(BuildTypeProbeGenome(Genome.NodeTypeInvert))
            .Evaluate(new float[Genome.SensorCount]);

        Assert.NotEqual(sigmoid.MoveDrive, linear.MoveDrive);
        Assert.True(invert.MoveDrive < 0f);
    }

    private static string BuildTypeProbeGenome(int type)
    {
        var chars = new char[Genome.Length];
        Array.Fill(chars, '0');
        chars[Genome.Bias1Offset] = 'F';
        chars[Genome.Type1Offset] = type.ToString("X", CultureInfo.InvariantCulture)[0];
        chars[Genome.Weights1Offset] = '7';
        chars[Genome.Bias2Offset] = '8';
        chars[Genome.Type2Offset] = '1';
        chars[Genome.Weights2Offset] = '7';
        chars[Genome.Bias3Offset] = '8';
        return new string(chars);
    }

    [Fact]
    public void Mutate_ZeroRate_IsIdentityAndReportsNoChanges()
    {
        var parent = Genome.Random(new Random(7));
        var child = Genome.Mutate(parent, 0d, new Random(1), out var changes);

        Assert.Equal(parent, child);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Mutate_FullRate_ChangesAtLeastOneDigit()
    {
        var parent = Genome.Random(new Random(8));
        var child = Genome.Mutate(parent, 1d, new Random(2), out var changes);

        Assert.True(Genome.IsValid(child));
        Assert.True(changes > 0);
    }

    [Fact]
    public void Mutate_CanLeaveChildUnchangedAtNormalRate()
    {
        // With 1.5% per digit, identical children are common; pinned through a
        // deterministic seed so the "may not change" behavior cannot regress to
        // forced mutation.
        var parent = Genome.Random(new Random(10));
        string? unchanged = null;
        var found = false;
        for (var seed = 1; seed < 10_000; seed++)
        {
            var candidate = Genome.Mutate(parent, Tuning.DefaultMutationRate, new Random(seed), out var changes);
            if (changes != 0)
                continue;
            unchanged = candidate;
            found = true;
            break;
        }

        Assert.True(found, "No deterministic seed produced an unchanged child; mutation is forced.");
        Assert.Equal(parent, unchanged);
    }

    [Fact]
    public void Decode_RejectsMalformedGenomes()
    {
        Assert.Throws<ArgumentException>(() => Genome.Decode("ABC"));
        Assert.Throws<ArgumentException>(() => Genome.Decode(new string('G', Genome.Length)));
    }

    [Fact]
    public void Decode_Evaluate_ProducesFiniteDrives()
    {
        var brain = Genome.Decode(Genome.Random(new Random(12)));
        var output = brain.Evaluate([0.2f, 0.5f, 0.1f, 0.8f, 0.3f, 0.6f, 0.0f, 1.0f]);

        Assert.True(float.IsFinite(output.MoveDrive));
        Assert.True(float.IsFinite(output.TurnDrive));
        Assert.InRange(output.MoveDrive, -1f, 1f);
        Assert.InRange(output.TurnDrive, -1f, 1f);
        Assert.Equal(Genome.Hidden1Count, brain.Hidden1.Length);
        Assert.Equal(Genome.Hidden2Count, brain.Hidden2.Length);
    }
}
