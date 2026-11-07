using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Fixed latent neural genome. The genome always stores the maximum lattice:
///     12 sensor slots, 3 hidden layers of 8 nodes, and 4 output slots. The body
///     phenotype selects the active slices, so a body mutation can never make the
///     neural genome malformed or length-incompatible.
/// </summary>
public static class NeuralGenome
{
    /// <summary>Maximum semantic sensor slots.</summary>
    public const int SensorSlots = 12;

    /// <summary>Maximum hidden nodes per layer.</summary>
    public const int HiddenSlots = 8;

    /// <summary>Maximum hidden layers.</summary>
    public const int HiddenLayerSlots = 3;

    /// <summary>Maximum semantic output slots.</summary>
    public const int OutputSlots = 4;

    /// <summary>Input-to-first-hidden weights.</summary>
    public const int WeightsInputOffset = 0;

    /// <summary>Input-to-first-hidden weight count.</summary>
    public const int WeightsInputLength = SensorSlots * HiddenSlots;

    /// <summary>First-to-second hidden weights.</summary>
    public const int WeightsHidden12Offset = WeightsInputOffset + WeightsInputLength;

    /// <summary>First-to-second hidden weight count.</summary>
    public const int WeightsHidden12Length = HiddenSlots * HiddenSlots;

    /// <summary>Second-to-third hidden weights.</summary>
    public const int WeightsHidden23Offset = WeightsHidden12Offset + WeightsHidden12Length;

    /// <summary>Second-to-third hidden weight count.</summary>
    public const int WeightsHidden23Length = HiddenSlots * HiddenSlots;

    /// <summary>First-hidden-to-output weights.</summary>
    public const int WeightsHidden1OutOffset = WeightsHidden23Offset + WeightsHidden23Length;

    /// <summary>First-hidden-to-output weight count.</summary>
    public const int WeightsHidden1OutLength = HiddenSlots * OutputSlots;

    /// <summary>Second-hidden-to-output weights.</summary>
    public const int WeightsHidden2OutOffset = WeightsHidden1OutOffset + WeightsHidden1OutLength;

    /// <summary>Second-hidden-to-output weight count.</summary>
    public const int WeightsHidden2OutLength = HiddenSlots * OutputSlots;

    /// <summary>Third-hidden-to-output weights.</summary>
    public const int WeightsHidden3OutOffset = WeightsHidden2OutOffset + WeightsHidden2OutLength;

    /// <summary>Third-hidden-to-output weight count.</summary>
    public const int WeightsHidden3OutLength = HiddenSlots * OutputSlots;

    /// <summary>Activation-type offset for layer one.</summary>
    public const int Types1Offset = WeightsHidden3OutOffset + WeightsHidden3OutLength;

    /// <summary>Activation-type offset for layer two.</summary>
    public const int Types2Offset = Types1Offset + HiddenSlots;

    /// <summary>Activation-type offset for layer three.</summary>
    public const int Types3Offset = Types2Offset + HiddenSlots;

    /// <summary>Bias offset for layer one.</summary>
    public const int Bias1Offset = Types3Offset + HiddenSlots;

    /// <summary>Bias offset for layer two.</summary>
    public const int Bias2Offset = Bias1Offset + HiddenSlots;

    /// <summary>Bias offset for layer three.</summary>
    public const int Bias3Offset = Bias2Offset + HiddenSlots;

    /// <summary>Output bias offset.</summary>
    public const int OutputBiasOffset = Bias3Offset + HiddenSlots;

    /// <summary>Complete fixed neural-genome length.</summary>
    public const int Length = OutputBiasOffset + OutputSlots;

    /// <summary>Hidden-node activation type: standard sigmoid.</summary>
    public const int NodeTypeSigmoid = 0;

    /// <summary>Hidden-node activation type: linear pass-through.</summary>
    public const int NodeTypeLinear = 1;

    /// <summary>Hidden-node activation type: inverted pass-through.</summary>
    public const int NodeTypeInvert = 2;

    /// <summary>Hidden-node activation type: rectified linear unit.</summary>
    public const int NodeTypeRelu = 3;

    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>True when the string is a well-formed fixed-latent neural genome.</summary>
    public static bool IsValid(string? genome)
    {
        if (genome is null || genome.Length != Length)
            return false;
        foreach (var character in genome)
            if (!Uri.IsHexDigit(character))
                return false;
        return true;
    }

    /// <summary>Creates a zero-filled neural genome (silent but structurally valid).</summary>
    public static string CreateBlank() => new('0', Length);

    /// <summary>
    ///     Builds the calibrated founder neural genome by placing the legacy
    ///     8 -> 5 -> 5 -> 3 network into the active latent slices. All latent
    ///     weights remain zero.
    /// </summary>
    public static string CalibratedFounderTemplate()
    {
        var legacy = Genome.CalibratedFounderTemplate().ToUpperInvariant();
        var chars = new char[Length];
        Array.Fill(chars, '0');

        for (var sensor = 0; sensor < Genome.SensorCount; sensor++)
        {
            for (var hidden = 0; hidden < Genome.Hidden1Count; hidden++)
            {
                chars[WeightsInputOffset + (sensor * HiddenSlots) + hidden] =
                    legacy[Genome.Weights0Offset + (sensor * Genome.Hidden1Count) + hidden];
            }
        }

        for (var first = 0; first < Genome.Hidden1Count; first++)
        {
            for (var second = 0; second < Genome.Hidden2Count; second++)
            {
                chars[WeightsHidden12Offset + (first * HiddenSlots) + second] =
                    legacy[Genome.Weights1Offset + (first * Genome.Hidden2Count) + second];
            }
        }

        // The default body uses two hidden layers, so layer-two output
        // projection carries the calibrated 5 -> 3 weights.
        for (var second = 0; second < Genome.Hidden2Count; second++)
        {
            for (var output = 0; output < Genome.OutputCount; output++)
            {
                chars[WeightsHidden2OutOffset + (second * OutputSlots) + output] =
                    legacy[Genome.Weights2Offset + (second * Genome.OutputCount) + output];
            }
        }

        for (var hidden = 0; hidden < Genome.Hidden1Count; hidden++)
        {
            chars[Types1Offset + hidden] = legacy[Genome.Type1Offset + hidden];
            chars[Bias1Offset + hidden] = legacy[Genome.Bias1Offset + hidden];
            chars[Types2Offset + hidden] = legacy[Genome.Type2Offset + hidden];
            chars[Bias2Offset + hidden] = legacy[Genome.Bias2Offset + hidden];
        }

        for (var output = 0; output < Genome.OutputCount; output++)
            chars[OutputBiasOffset + output] = legacy[Genome.Bias3Offset + output];

        return new string(chars);
    }

    /// <summary>Creates a calibrated founder genome with a broad neural mutation pass.</summary>
    public static string RandomFounder(Random random) =>
        Mutate(CalibratedFounderTemplate(), 0.01d, random, out _);

    /// <summary>Creates a fully random but structurally valid latent neural genome.</summary>
    public static string Random(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var chars = new char[Length];
        for (var index = 0; index < chars.Length; index++)
            chars[index] = HexDigits[random.Next(16)];
        return new string(chars);
    }

    /// <summary>Applies fixed-position nibble-step mutations to a neural genome.</summary>
    /// <returns>The mutated genome and number of changed digits.</returns>
    public static string Mutate(string genome, double rate, Random random, out int changes)
    {
        ArgumentNullException.ThrowIfNull(genome);
        ArgumentNullException.ThrowIfNull(random);
        if (!IsValid(genome))
            throw new ArgumentException("Neural genome is not valid.", nameof(genome));
        if (rate is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(rate));

        var chars = genome.ToUpperInvariant().ToCharArray();
        changes = 0;
        for (var index = 0; index < chars.Length; index++)
        {
            if (random.NextDouble() >= rate)
                continue;
            var value = ToNibble(chars[index]);
            value = (value + (random.Next(2) == 0 ? 1 : 15)) % 16;
            chars[index] = HexDigits[value];
            changes++;
        }

        return new string(chars);
    }

    /// <summary>Decodes a valid neural genome into an evaluable fixed lattice.</summary>
    /// <exception cref="ArgumentException">Thrown when the genome is malformed.</exception>
    public static NeuralBrain Decode(string genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        if (!IsValid(genome))
            throw new ArgumentException("Neural genome is not valid.", nameof(genome));

        var upper = genome.ToUpperInvariant();
        var weightsInput = new float[WeightsInputLength];
        var weights12 = new float[WeightsHidden12Length];
        var weights23 = new float[WeightsHidden23Length];
        var weights1Out = new float[WeightsHidden1OutLength];
        var weights2Out = new float[WeightsHidden2OutLength];
        var weights3Out = new float[WeightsHidden3OutLength];
        var types1 = new int[HiddenSlots];
        var types2 = new int[HiddenSlots];
        var types3 = new int[HiddenSlots];
        var bias1 = new float[HiddenSlots];
        var bias2 = new float[HiddenSlots];
        var bias3 = new float[HiddenSlots];
        var outputBias = new float[OutputSlots];

        for (var i = 0; i < weightsInput.Length; i++)
            weightsInput[i] = DecodeWeight(ToNibble(upper[WeightsInputOffset + i]));
        for (var i = 0; i < weights12.Length; i++)
            weights12[i] = DecodeWeight(ToNibble(upper[WeightsHidden12Offset + i]));
        for (var i = 0; i < weights23.Length; i++)
            weights23[i] = DecodeWeight(ToNibble(upper[WeightsHidden23Offset + i]));
        for (var i = 0; i < weights1Out.Length; i++)
            weights1Out[i] = DecodeWeight(ToNibble(upper[WeightsHidden1OutOffset + i]));
        for (var i = 0; i < weights2Out.Length; i++)
            weights2Out[i] = DecodeWeight(ToNibble(upper[WeightsHidden2OutOffset + i]));
        for (var i = 0; i < weights3Out.Length; i++)
            weights3Out[i] = DecodeWeight(ToNibble(upper[WeightsHidden3OutOffset + i]));
        for (var i = 0; i < HiddenSlots; i++)
        {
            types1[i] = ToNibble(upper[Types1Offset + i]) & 3;
            types2[i] = ToNibble(upper[Types2Offset + i]) & 3;
            types3[i] = ToNibble(upper[Types3Offset + i]) & 3;
            bias1[i] = DecodeBias(ToNibble(upper[Bias1Offset + i]));
            bias2[i] = DecodeBias(ToNibble(upper[Bias2Offset + i]));
            bias3[i] = DecodeBias(ToNibble(upper[Bias3Offset + i]));
        }

        for (var i = 0; i < outputBias.Length; i++)
            outputBias[i] = DecodeBias(ToNibble(upper[OutputBiasOffset + i]));

        return new NeuralBrain(
            weightsInput,
            weights12,
            weights23,
            weights1Out,
            weights2Out,
            weights3Out,
            types1,
            types2,
            types3,
            bias1,
            bias2,
            bias3,
            outputBias);
    }

    private static int ToNibble(char character)
    {
        var upper = char.ToUpperInvariant(character);
        return upper switch
        {
            >= '0' and <= '9' => upper - '0',
            >= 'A' and <= 'F' => upper - 'A' + 10,
            _ => throw new ArgumentException("Neural genome contains a non-hexadecimal character."),
        };
    }

    private static float DecodeWeight(int nibble) => nibble switch
    {
        0 => 0f,
        <= 7 => nibble / 7f,
        _ => -(nibble - 7) / 8f,
    };

    private static float DecodeBias(int nibble) => (nibble - 7.5f) / 7.5f;
}

/// <summary>
///     Evaluates the fixed latent neural lattice for one body phenotype. Body
///     masks select sensor/output slots, hidden-layer count, and active widths.
/// </summary>
public sealed class NeuralBrain
{
    private readonly float[] _weightsInput;
    private readonly float[] _weights12;
    private readonly float[] _weights23;
    private readonly float[] _weights1Out;
    private readonly float[] _weights2Out;
    private readonly float[] _weights3Out;
    private readonly int[] _types1;
    private readonly int[] _types2;
    private readonly int[] _types3;
    private readonly float[] _bias1;
    private readonly float[] _bias2;
    private readonly float[] _bias3;
    private readonly float[] _outputBias;
    private readonly float[] _hidden1 = new float[NeuralGenome.HiddenSlots];
    private readonly float[] _hidden2 = new float[NeuralGenome.HiddenSlots];
    private readonly float[] _hidden3 = new float[NeuralGenome.HiddenSlots];

    internal NeuralBrain(
        float[] weightsInput,
        float[] weights12,
        float[] weights23,
        float[] weights1Out,
        float[] weights2Out,
        float[] weights3Out,
        int[] types1,
        int[] types2,
        int[] types3,
        float[] bias1,
        float[] bias2,
        float[] bias3,
        float[] outputBias)
    {
        _weightsInput = weightsInput;
        _weights12 = weights12;
        _weights23 = weights23;
        _weights1Out = weights1Out;
        _weights2Out = weights2Out;
        _weights3Out = weights3Out;
        _types1 = types1;
        _types2 = types2;
        _types3 = types3;
        _bias1 = bias1;
        _bias2 = bias2;
        _bias3 = bias3;
        _outputBias = outputBias;
    }

    /// <summary>Evaluates one forward pass for the given body phenotype.</summary>
    /// <exception cref="ArgumentException">Thrown when the sensor vector has the wrong length.</exception>
    public BrainOutput Evaluate(BodyPhenotype body, ReadOnlySpan<float> sensors)
    {
        if (sensors.Length != NeuralGenome.SensorSlots)
            throw new ArgumentException($"Expected {NeuralGenome.SensorSlots} sensors.", nameof(sensors));

        var layers = Math.Clamp(body.HiddenLayers, 1, NeuralGenome.HiddenLayerSlots);
        var width1 = Math.Clamp(body.HiddenWidth1, 1, NeuralGenome.HiddenSlots);
        var width2 = Math.Clamp(body.HiddenWidth2, 1, NeuralGenome.HiddenSlots);
        var width3 = Math.Clamp(body.HiddenWidth3, 1, NeuralGenome.HiddenSlots);

        for (var node = 0; node < width1; node++)
        {
            var sum = _bias1[node];
            for (var sensor = 0; sensor < NeuralGenome.SensorSlots; sensor++)
                if (IsSensorActive(body, sensor))
                    sum += sensors[sensor] * _weightsInput[(sensor * NeuralGenome.HiddenSlots) + node];
            _hidden1[node] = Activate(sum, _types1[node]);
        }

        if (layers >= 2)
        {
            for (var node = 0; node < width2; node++)
            {
                var sum = _bias2[node];
                for (var previous = 0; previous < width1; previous++)
                    sum += _hidden1[previous] * _weights12[(previous * NeuralGenome.HiddenSlots) + node];
                _hidden2[node] = Activate(sum, _types2[node]);
            }
        }

        if (layers >= 3)
        {
            for (var node = 0; node < width3; node++)
            {
                var sum = _bias3[node];
                for (var previous = 0; previous < width2; previous++)
                    sum += _hidden2[previous] * _weights23[(previous * NeuralGenome.HiddenSlots) + node];
                _hidden3[node] = Activate(sum, _types3[node]);
            }
        }

        var lastWidth = layers switch { 1 => width1, 2 => width2, _ => width3 };
        var outputWeights = layers switch
        {
            1 => _weights1Out,
            2 => _weights2Out,
            _ => _weights3Out,
        };
        var lastHidden = layers switch { 1 => _hidden1, 2 => _hidden2, _ => _hidden3 };

        var move = _outputBias[(int)OutputId.Move];
        var turn = _outputBias[(int)OutputId.Turn];
        var spike = _outputBias[(int)OutputId.Spike];
        for (var node = 0; node < lastWidth; node++)
        {
            move += lastHidden[node] * outputWeights[(node * NeuralGenome.OutputSlots) + (int)OutputId.Move];
            turn += lastHidden[node] * outputWeights[(node * NeuralGenome.OutputSlots) + (int)OutputId.Turn];
            spike += lastHidden[node] * outputWeights[(node * NeuralGenome.OutputSlots) + (int)OutputId.Spike];
        }

        return new BrainOutput(
            MathF.Tanh(move),
            MathF.Tanh(turn),
            body.SpikeOrgan ? MathF.Tanh(spike) : 0f);
    }

    /// <summary>Read-only evaluated first hidden layer for debugging.</summary>
    public ReadOnlySpan<float> Hidden1 => _hidden1;

    /// <summary>Read-only evaluated second hidden layer for debugging.</summary>
    public ReadOnlySpan<float> Hidden2 => _hidden2;

    /// <summary>Read-only evaluated third hidden layer for debugging.</summary>
    public ReadOnlySpan<float> Hidden3 => _hidden3;

    private static bool IsSensorActive(BodyPhenotype body, int slot) => (SensorId)slot switch
    {
        SensorId.FoodAhead or SensorId.FoodLeft or SensorId.FoodRight => body.VisionOrgan,
        SensorId.CreatureAhead => body.CreatureSenseOrgan,
        SensorId.WallAhead or SensorId.Energy => true,
        SensorId.Sin or SensorId.Cos => body.RhythmOrgan,
        _ => false,
    };

    private static float Activate(float value, int type) => type switch
    {
        NeuralGenome.NodeTypeLinear => value,
        NeuralGenome.NodeTypeInvert => -value,
        NeuralGenome.NodeTypeRelu => MathF.Max(0f, value),
        _ => 1f / (1f + MathF.Exp(-value)),
    };
}
