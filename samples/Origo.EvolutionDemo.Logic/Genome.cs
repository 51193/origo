using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Fixed neural-network genome layout for the demo:
///     8 sensors → 5 hidden nodes → 5 hidden nodes → 3 outputs.
///     Every edge, bias, and hidden activation type is one hexadecimal digit,
///     so the complete genome is 103 characters long and point mutations can
///     reshape both wiring and node roles.
/// </summary>
public static class Genome
{
    public const int SensorCount = 8;
    public const int Hidden1Count = 5;
    public const int Hidden2Count = 5;
    public const int OutputCount = 3;

    public const int Weights0Offset = 0;
    public const int Weights0Length = SensorCount * Hidden1Count;
    public const int Type1Offset = Weights0Offset + Weights0Length;
    public const int Type1Length = Hidden1Count;
    public const int Bias1Offset = Type1Offset + Type1Length;
    public const int Bias1Length = Hidden1Count;
    public const int Weights1Offset = Bias1Offset + Bias1Length;
    public const int Weights1Length = Hidden1Count * Hidden2Count;
    public const int Type2Offset = Weights1Offset + Weights1Length;
    public const int Type2Length = Hidden2Count;
    public const int Bias2Offset = Type2Offset + Type2Length;
    public const int Bias2Length = Hidden2Count;
    public const int Weights2Offset = Bias2Offset + Bias2Length;
    public const int Weights2Length = Hidden2Count * OutputCount;
    public const int Bias3Offset = Weights2Offset + Weights2Length;
    public const int Bias3Length = OutputCount;
    public const int Length = Bias3Offset + Bias3Length;

    /// <summary>Hidden-node activation type: standard sigmoid.</summary>
    public const int NodeTypeSigmoid = 0;

    /// <summary>Hidden-node activation type: pass the weighted sum through unchanged.</summary>
    public const int NodeTypeLinear = 1;

    /// <summary>Hidden-node activation type: negate the weighted sum (inhibitory relay).</summary>
    public const int NodeTypeInvert = 2;

    /// <summary>Hidden-node activation type: rectified linear unit.</summary>
    public const int NodeTypeRelu = 3;

    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>True when the value is a well-formed genome of the expected length.</summary>
    public static bool IsValid(string? genome)
    {
        if (genome is null || genome.Length != Length)
            return false;

        foreach (var digit in genome)
        {
            if (!Uri.IsHexDigit(digit))
                return false;
        }

        return true;
    }

    /// <summary>Creates a sparse random genome with at least one output edge.</summary>
    public static string Random(Random random, double edgeProbability = 0.38)
    {
        ArgumentNullException.ThrowIfNull(random);

        var chars = new char[Length];
        FillEdges(chars, Weights0Offset, Weights0Length, random, edgeProbability);
        FillTypes(chars, Type1Offset, Type1Length, random);
        FillBiases(chars, Bias1Offset, Bias1Length, random);
        FillEdges(chars, Weights1Offset, Weights1Length, random, edgeProbability);
        FillTypes(chars, Type2Offset, Type2Length, random);
        FillBiases(chars, Bias2Offset, Bias2Length, random);
        FillEdges(chars, Weights2Offset, Weights2Length, random, edgeProbability);
        FillBiases(chars, Bias3Offset, Bias3Length, random);

        // A genome with no output edges can never act. Keep random startup
        // varied while guaranteeing every starting creature has a chance.
        if (chars.AsSpan(Weights2Offset, Weights2Length).IndexOfAnyExcept('0') < 0)
            chars[Weights2Offset + random.Next(Weights2Length)] =
                HexDigits[random.Next(1, HexDigits.Length)];

        return new string(chars);
    }

    /// <summary>
    ///     Calibrated starter brain used for every founder. It is deliberately
    ///     simple but functional: food ahead raises the forward drive, food on
    ///     one side turns toward it, a wall ahead turns away, and a weak 6-second
    ///     rhythm adds a little exploration. Founder genomes are this template
    ///     plus a larger-than-usual random mutation set, so first-generation
    ///     creatures are diverse but not helpless.
    /// </summary>
    public static string CalibratedFounderTemplate()
    {
        var chars = new char[Length];
        Array.Fill(chars, '0');

        // Neutral small biases and linear hidden nodes make the template easy
        // to reason about; mutation is free to change any type later.
        SetNibbles(chars, Type1Offset, Type1Length, NodeTypeLinear);
        SetNibbles(chars, Bias1Offset, Bias1Length, 8);
        SetNibbles(chars, Type2Offset, Type2Length, NodeTypeLinear);
        SetNibbles(chars, Bias2Offset, Bias2Length, 8);

        // H1: food-ahead, food-left, food-right, wall-ahead, sin(clock).
        SetWeight(chars, sensor: 0, hidden1: 0, 7);
        SetWeight(chars, sensor: 1, hidden1: 1, 7);
        SetWeight(chars, sensor: 2, hidden1: 2, 7);
        SetWeight(chars, sensor: 3, hidden1: 3, 7);
        SetWeight(chars, sensor: 6, hidden1: 4, 7);

        // H2: food-ahead, (right - left), wall, rhythm, rhythm.
        SetWeight1(chars, hidden1: 0, hidden2: 0, 7);
        SetWeight1(chars, hidden1: 2, hidden2: 1, 7);
        SetWeight1(chars, hidden1: 1, hidden2: 1, 15);
        SetWeight1(chars, hidden1: 3, hidden2: 2, 7);
        SetWeight1(chars, hidden1: 4, hidden2: 3, 7);
        SetWeight1(chars, hidden1: 4, hidden2: 4, 7);

        // Move forward with a strong constant bias; steer toward food, away
        // from walls, and add a small rhythmic wobble.
        SetWeight2(chars, hidden2: 0, output: 0, 7);
        SetWeight2(chars, hidden2: 4, output: 0, 2);
        SetWeight2(chars, hidden2: 1, output: 1, 7);
        SetWeight2(chars, hidden2: 3, output: 1, 2);
        chars[Bias3Offset] = 'F';
        chars[Bias3Offset + 1] = '8';
        // Keep the new spike action silent for calibrated founders; mutation
        // can later discover it as a predatory strategy.
        chars[Bias3Offset + 2] = '7';
        return new string(chars);
    }

    /// <summary>
    ///     Creates a viable founder from the calibrated template, then applies
    ///     a broad founder-mutation pass. The output move bias is restored so a
    ///     mutated founder still creeps forward even if the steering edges
    ///     change.
    /// </summary>
    public static string RandomFounder(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var mutated = Mutate(
            CalibratedFounderTemplate(),
            Tuning.FounderMutationRate,
            random,
            out _).ToCharArray();
        mutated[Bias3Offset] = 'F';
        return new string(mutated);
    }

    private static void SetNibbles(char[] chars, int offset, int length, int nibble)
    {
        var hex = HexDigits[nibble];
        for (var i = 0; i < length; i++)
            chars[offset + i] = hex;
    }

    private static void SetWeight(char[] chars, int sensor, int hidden1, int nibble) =>
        chars[Weights0Offset + (sensor * Hidden1Count) + hidden1] = HexDigits[nibble];

    private static void SetWeight1(char[] chars, int hidden1, int hidden2, int nibble) =>
        chars[Weights1Offset + (hidden1 * Hidden2Count) + hidden2] = HexDigits[nibble];

    private static void SetWeight2(char[] chars, int hidden2, int output, int nibble) =>
        chars[Weights2Offset + (hidden2 * OutputCount) + output] = HexDigits[nibble];

    /// <summary>
    ///     Applies independent point mutations. Each hexadecimal digit changes
    ///     with <paramref name="rate" />; a child may be identical to its parent.
    /// </summary>
    /// <returns>The child genome and the number of changed digits.</returns>
    public static string Mutate(string genome, double rate, Random random, out int changes)
    {
        ArgumentNullException.ThrowIfNull(genome);
        if (!IsValid(genome))
            throw new ArgumentException("Genome is not valid.", nameof(genome));
        ArgumentNullException.ThrowIfNull(random);
        if (rate is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(rate), "Mutation rate must be between 0 and 1.");

        var chars = genome.ToUpperInvariant().ToCharArray();
        changes = 0;
        for (var i = 0; i < chars.Length; i++)
        {
            if (random.NextDouble() >= rate)
                continue;

            var replacement = HexDigits[random.Next(HexDigits.Length)];
            if (replacement == chars[i])
                continue;

            chars[i] = replacement;
            changes++;
        }

        return new string(chars);
    }

    /// <summary>Decodes a genome into a forward-evaluation network.</summary>
    /// <exception cref="ArgumentException">Thrown when the genome is malformed.</exception>
    public static Brain Decode(string genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        if (!IsValid(genome))
            throw new ArgumentException("Genome is not valid.", nameof(genome));

        var upper = genome.ToUpperInvariant();
        var weights0 = new float[Weights0Length];
        var types1 = new int[Type1Length];
        var bias1 = new float[Bias1Length];
        var weights1 = new float[Weights1Length];
        var types2 = new int[Type2Length];
        var bias2 = new float[Bias2Length];
        var weights2 = new float[Weights2Length];
        var bias3 = new float[Bias3Length];

        for (var i = 0; i < weights0.Length; i++)
            weights0[i] = DecodeWeight(ToNibble(upper[Weights0Offset + i]));
        for (var i = 0; i < types1.Length; i++)
            types1[i] = ToNibble(upper[Type1Offset + i]) % 4;
        for (var i = 0; i < bias1.Length; i++)
            bias1[i] = DecodeBias(ToNibble(upper[Bias1Offset + i]));
        for (var i = 0; i < weights1.Length; i++)
            weights1[i] = DecodeWeight(ToNibble(upper[Weights1Offset + i]));
        for (var i = 0; i < types2.Length; i++)
            types2[i] = ToNibble(upper[Type2Offset + i]) % 4;
        for (var i = 0; i < bias2.Length; i++)
            bias2[i] = DecodeBias(ToNibble(upper[Bias2Offset + i]));
        for (var i = 0; i < weights2.Length; i++)
            weights2[i] = DecodeWeight(ToNibble(upper[Weights2Offset + i]));
        for (var i = 0; i < bias3.Length; i++)
            bias3[i] = DecodeBias(ToNibble(upper[Bias3Offset + i]));

        return new Brain(weights0, types1, bias1, weights1, types2, bias2, weights2, bias3);
    }

    private static void FillEdges(
        char[] chars,
        int offset,
        int length,
        Random random,
        double edgeProbability)
    {
        for (var i = 0; i < length; i++)
        {
            chars[offset + i] = random.NextDouble() < edgeProbability
                ? HexDigits[random.Next(1, HexDigits.Length)]
                : '0';
        }
    }

    private static void FillTypes(char[] chars, int offset, int length, Random random)
    {
        for (var i = 0; i < length; i++)
            chars[offset + i] = HexDigits[random.Next(4)];
    }

    private static void FillBiases(char[] chars, int offset, int length, Random random)
    {
        for (var i = 0; i < length; i++)
            chars[offset + i] = HexDigits[random.Next(HexDigits.Length)];
    }

    private static int ToNibble(char digit) => digit switch
    {
        >= '0' and <= '9' => digit - '0',
        >= 'A' and <= 'F' => digit - 'A' + 10,
        _ => throw new ArgumentException("Genome contains a non-hexadecimal character.", nameof(digit)),
    };

    private static float DecodeWeight(int nibble) => nibble switch
    {
        0 => 0f,
        <= 7 => nibble / 7f,
        _ => -(nibble - 7) / 8f,
    };

    private static float DecodeBias(int nibble) => (nibble - 7.5f) / 7.5f;
}

/// <summary>Forward-evaluated result of one creature's genome.</summary>
public readonly record struct BrainOutput(float MoveDrive, float TurnDrive, float SpikeDrive);

/// <summary>
///     Immutable decoded network. Evaluation uses caller-provided sensor values
///     and returns the three action drives; hidden nodes may use different
///     activation functions from their type genes.
/// </summary>
public sealed class Brain
{
    private readonly float[] _weights0;
    private readonly int[] _types1;
    private readonly float[] _bias1;
    private readonly float[] _weights1;
    private readonly int[] _types2;
    private readonly float[] _bias2;
    private readonly float[] _weights2;
    private readonly float[] _bias3;
    private readonly float[] _hidden1 = new float[Genome.Hidden1Count];
    private readonly float[] _hidden2 = new float[Genome.Hidden2Count];

    internal Brain(
        float[] weights0,
        int[] types1,
        float[] bias1,
        float[] weights1,
        int[] types2,
        float[] bias2,
        float[] weights2,
        float[] bias3)
    {
        _weights0 = weights0;
        _types1 = types1;
        _bias1 = bias1;
        _weights1 = weights1;
        _types2 = types2;
        _bias2 = bias2;
        _weights2 = weights2;
        _bias3 = bias3;
    }

    /// <summary>Evaluates one forward pass.</summary>
    /// <exception cref="ArgumentException">Thrown when the sensor vector has the wrong length.</exception>
    public BrainOutput Evaluate(ReadOnlySpan<float> sensors)
    {
        if (sensors.Length != Genome.SensorCount)
            throw new ArgumentException($"Expected {Genome.SensorCount} sensors.", nameof(sensors));

        for (var j = 0; j < Genome.Hidden1Count; j++)
        {
            var sum = _bias1[j];
            for (var i = 0; i < Genome.SensorCount; i++)
                sum += sensors[i] * _weights0[(i * Genome.Hidden1Count) + j];
            _hidden1[j] = Activate(sum, _types1[j]);
        }

        for (var j = 0; j < Genome.Hidden2Count; j++)
        {
            var sum = _bias2[j];
            for (var i = 0; i < Genome.Hidden1Count; i++)
                sum += _hidden1[i] * _weights1[(i * Genome.Hidden2Count) + j];
            _hidden2[j] = Activate(sum, _types2[j]);
        }

        var move = _bias3[0];
        var turn = _bias3[1];
        var spike = _bias3[2];
        for (var i = 0; i < Genome.Hidden2Count; i++)
        {
            move += _hidden2[i] * _weights2[(i * Genome.OutputCount) + 0];
            turn += _hidden2[i] * _weights2[(i * Genome.OutputCount) + 1];
            spike += _hidden2[i] * _weights2[(i * Genome.OutputCount) + 2];
        }

        return new BrainOutput(MathF.Tanh(move), MathF.Tanh(turn), MathF.Tanh(spike));
    }

    /// <summary>Read-only access to the evaluated first hidden layer (for debugging/UI).</summary>
    public ReadOnlySpan<float> Hidden1 => _hidden1;

    /// <summary>Read-only access to the evaluated second hidden layer (for debugging/UI).</summary>
    public ReadOnlySpan<float> Hidden2 => _hidden2;

    private static float Activate(float value, int type) => type switch
    {
        Genome.NodeTypeLinear => value,
        Genome.NodeTypeInvert => -value,
        Genome.NodeTypeRelu => MathF.Max(0f, value),
        _ => 1f / (1f + MathF.Exp(-value)),
    };
}
