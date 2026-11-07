using System;
using System.Collections.Generic;
using System.Globalization;

namespace Origo.EvolutionDemo.Logic;

/// <summary>A body organ encoded by an organ codon.</summary>
public enum BodyOrgan
{
    /// <summary>Enables the spike action and its range.</summary>
    Spike = 0,

    /// <summary>Enables the sine/cosine rhythm sensors.</summary>
    Rhythm = 1,

    /// <summary>Enables the three food-direction sensors.</summary>
    Vision = 2,

    /// <summary>Enables the forward creature sensor.</summary>
    CreatureSense = 3,
}

/// <summary>Stable semantic sensor slots in the latent brain.</summary>
public enum SensorId
{
    /// <summary>Best food proximity in the forward cone.</summary>
    FoodAhead = 0,

    /// <summary>Best food proximity in the left cone.</summary>
    FoodLeft = 1,

    /// <summary>Best food proximity in the right cone.</summary>
    FoodRight = 2,

    /// <summary>Best creature proximity in the forward cone.</summary>
    CreatureAhead = 3,

    /// <summary>Distance to the pond wall ahead.</summary>
    WallAhead = 4,

    /// <summary>Normalized stored energy.</summary>
    Energy = 5,

    /// <summary>Sine of the creature's own oscillator phase.</summary>
    Sin = 6,

    /// <summary>Cosine of the creature's own oscillator phase.</summary>
    Cos = 7,

    /// <summary>Reserved future sensor slot A.</summary>
    FutureA = 8,

    /// <summary>Reserved future sensor slot B.</summary>
    FutureB = 9,

    /// <summary>Reserved future sensor slot C.</summary>
    FutureC = 10,

    /// <summary>Reserved future sensor slot D.</summary>
    FutureD = 11,
}

/// <summary>Stable semantic output slots in the latent brain.</summary>
public enum OutputId
{
    /// <summary>Forward movement drive.</summary>
    Move = 0,

    /// <summary>Turn drive.</summary>
    Turn = 1,

    /// <summary>Spike action drive.</summary>
    Spike = 2,

    /// <summary>Reserved future action slot.</summary>
    Future = 3,
}

/// <summary>
///     Decoded body phenotype. The active sensor/output masks tell the fixed
///     latent brain which slots participate in the current network.
/// </summary>
public readonly record struct BodyPhenotype(
    float SizeScale,
    float BodyRadius,
    float FoodPickupRadius,
    float MaxEnergy,
    float MoveSpeed,
    float TurnSpeed,
    float SenseRange,
    float SpikeRadius,
    float BasalMetabolism,
    float MaxAge,
    bool SpikeOrgan,
    bool RhythmOrgan,
    bool VisionOrgan,
    bool CreatureSenseOrgan,
    int HiddenLayers,
    int HiddenWidth1,
    int HiddenWidth2,
    int HiddenWidth3,
    int Pattern)
{
    /// <summary>Returns whether a semantic sensor slot is active for this body.</summary>
    public bool HasSensor(SensorId sensor) => sensor switch
    {
        SensorId.FoodAhead or SensorId.FoodLeft or SensorId.FoodRight => VisionOrgan,
        SensorId.CreatureAhead => CreatureSenseOrgan,
        SensorId.WallAhead or SensorId.Energy => true,
        SensorId.Sin or SensorId.Cos => RhythmOrgan,
        _ => false,
    };

    /// <summary>Returns whether a semantic output slot is active for this body.</summary>
    public bool HasOutput(OutputId output) => output switch
    {
        OutputId.Move or OutputId.Turn => true,
        OutputId.Spike => SpikeOrgan,
        _ => false,
    };

    /// <summary>Number of currently active sensor slots.</summary>
    public int ActiveSensorCount =>
        (VisionOrgan ? 3 : 0) + (CreatureSenseOrgan ? 1 : 0) + 2 + (RhythmOrgan ? 2 : 0);

    /// <summary>Number of currently active output slots.</summary>
    public int ActiveOutputCount => SpikeOrgan ? 3 : 2;
}

/// <summary>
///     Table-driven, sparse, variable-length body genome. Codons are three hex
///     characters: a four-bit opcode and an eight-bit argument. Unknown codons
///     are neutral. Numeric traits use the median of valid mappings; organs use
///     OR. This deliberately keeps only part of the arg space meaningful.
/// </summary>
public static class BodyGenome
{
    /// <summary>Hex characters per codon.</summary>
    public const int CodonLength = 3;

    /// <summary>Minimum number of codons in a viable body genome.</summary>
    public const int MinCodons = 8;

    /// <summary>Number of codons in the calibrated founder template.</summary>
    public const int BaseCodons = 24;

    /// <summary>Maximum number of codons in a body genome.</summary>
    public const int MaxCodons = 128;

    /// <summary>Default founder body template (24 codons, 72 hex digits).</summary>
    public const string DefaultTemplate =
        "13A242345430524637748818949A49B49000010020030C00" +
        "F00F00F00F00F00F00F00F00";

    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>Returns a default calibrated body genome.</summary>
    public static string CreateDefault() => DefaultTemplate;

    /// <summary>True when the raw body string contains only hex characters and is non-empty.</summary>
    public static bool IsStructurallyValid(string? body)
    {
        if (string.IsNullOrEmpty(body) || body.Length < MinCodons * CodonLength)
            return false;
        foreach (var character in body)
            if (!Uri.IsHexDigit(character))
                return false;
        return true;
    }

    /// <summary>Number of complete codons in a raw body genome.</summary>
    public static int CodonCount(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body.Length / CodonLength;
    }

    /// <summary>Expresses a body genome into its phenotype.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="body" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the body genome is malformed.</exception>
    public static BodyPhenotype Decode(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!IsStructurallyValid(body))
            throw new ArgumentException("Body genome is malformed.", nameof(body));

        var sizes = new List<float>();
        var speeds = new List<float>();
        var turns = new List<float>();
        var senses = new List<float>();
        var spikes = new List<float>();
        var metabolisms = new List<float>();
        var ages = new List<float>();
        var layers = new List<float>();
        var widths1 = new List<float>();
        var widths2 = new List<float>();
        var widths3 = new List<float>();
        var patterns = new List<float>();
        var organs = new bool[4];

        var codons = CodonCount(body);
        for (var index = 0; index < codons; index++)
        {
            var offset = index * CodonLength;
            var opcode = Nibble(body[offset]);
            var argument = (Nibble(body[offset + 1]) * 16) + Nibble(body[offset + 2]);

            switch (opcode)
            {
                case 0x0 when argument <= 0x3F:
                    organs[argument / 16] = true;
                    break;
                case 0x1 when argument <= 0xBF:
                    sizes.Add(Tuning.BodySizeMin + ((Tuning.BodySizeMax - Tuning.BodySizeMin) * argument / 191f));
                    break;
                case 0x2 when argument <= 0x9F:
                    speeds.Add(Tuning.BodySpeedMin + ((Tuning.BodySpeedMax - Tuning.BodySpeedMin) * argument / 159f));
                    break;
                case 0x3 when argument <= 0x9F:
                    turns.Add(Tuning.BodyTurnMin + ((Tuning.BodyTurnMax - Tuning.BodyTurnMin) * argument / 159f));
                    break;
                case 0x4 when argument <= 0x9F:
                    senses.Add(Tuning.BodySenseMin + ((Tuning.BodySenseMax - Tuning.BodySenseMin) * argument / 159f));
                    break;
                case 0x5 when argument <= 0x7F:
                    spikes.Add(Tuning.BodySpikeMin + ((Tuning.BodySpikeMax - Tuning.BodySpikeMin) * argument / 127f));
                    break;
                case 0x6 when argument <= 0x7F:
                    metabolisms.Add(Tuning.BodyMetabolismMin + ((Tuning.BodyMetabolismMax - Tuning.BodyMetabolismMin) * argument / 127f));
                    break;
                case 0x7 when argument <= 0xBF:
                    ages.Add(Tuning.BodyAgeMin + ((Tuning.BodyAgeMax - Tuning.BodyAgeMin) * argument / 191f));
                    break;
                case 0x8 when argument <= 0x2F:
                    layers.Add(1f + (argument / 16));
                    break;
                case 0x9 when argument <= 0x7F:
                    widths1.Add(1f + (argument / 16));
                    break;
                case 0xA when argument <= 0x7F:
                    widths2.Add(1f + (argument / 16));
                    break;
                case 0xB when argument <= 0x7F:
                    widths3.Add(1f + (argument / 16));
                    break;
                case 0xC when argument <= 0xBF:
                    patterns.Add(argument % CreatureAppearance.PatternCount);
                    break;
                default:
                    break;
            }
        }

        var sizeScale = Median(sizes, 1f);
        var radius = Tuning.BodyRadiusBase * sizeScale;
        var hiddenLayers = (int)Math.Clamp(MathF.Round(Median(layers, 2f)), 1f, 3f);
        return new BodyPhenotype(
            sizeScale,
            radius,
            radius + Tuning.FoodPickupPadding,
            Tuning.BodyMaxEnergyBase + (Tuning.BodyMaxEnergyPerSize * sizeScale * sizeScale),
            Median(speeds, 55f),
            Median(turns, 2.8f),
            Median(senses, 120f),
            Median(spikes, Tuning.BodySpikeMin),
            Median(metabolisms, Tuning.BodyMetabolismMin),
            Median(ages, Tuning.BodyAgeMin),
            organs[(int)BodyOrgan.Spike],
            organs[(int)BodyOrgan.Rhythm],
            organs[(int)BodyOrgan.Vision],
            organs[(int)BodyOrgan.CreatureSense],
            hiddenLayers,
            (int)Math.Clamp(MathF.Round(Median(widths1, 5f)), 1f, 8f),
            (int)Math.Clamp(MathF.Round(Median(widths2, 5f)), 1f, 8f),
            (int)Math.Clamp(MathF.Round(Median(widths3, 5f)), 1f, 8f),
            (int)MathF.Round(Median(patterns, 0f)));
    }

    /// <summary>Applies body nibble-step, codon-length, and optional frameshift mutations.</summary>
    /// <returns>The child body genome and the number of mutation events.</returns>
    /// <exception cref="ArgumentException">Thrown when the parent body is malformed.</exception>
    public static string Mutate(
        string body,
        double stepRate,
        double lengthRate,
        double frameshiftRate,
        Random random,
        out int changes)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(random);
        if (!IsStructurallyValid(body))
            throw new ArgumentException("Body genome is malformed.", nameof(body));
        if (stepRate is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(stepRate));
        if (lengthRate is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(lengthRate));
        if (frameshiftRate is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(frameshiftRate));

        var chars = new List<char>(body.ToUpperInvariant());
        changes = 0;

        for (var index = 0; index < chars.Count; index++)
        {
            if (random.NextDouble() >= stepRate)
                continue;
            var value = Nibble(chars[index]);
            value = (value + (random.Next(2) == 0 ? 1 : 15)) % 16;
            chars[index] = HexDigits[value];
            changes++;
        }

        if (random.NextDouble() < lengthRate)
        {
            var codonCount = chars.Count / CodonLength;
            if (codonCount > MinCodons && (codonCount >= MaxCodons || random.Next(2) == 0))
            {
                var start = random.Next(codonCount) * CodonLength;
                chars.RemoveRange(start, CodonLength);
                changes++;
            }
            else if (codonCount < MaxCodons)
            {
                var source = random.Next(codonCount) * CodonLength;
                var copy = chars.GetRange(source, CodonLength);
                var destination = random.Next(codonCount + 1) * CodonLength;
                chars.InsertRange(destination, copy);
                changes++;
            }
        }

        if (frameshiftRate > 0d
            && random.NextDouble() < frameshiftRate
            && chars.Count >= MinCodons * CodonLength)
        {
            var position = random.Next(chars.Count);
            if (random.Next(2) == 0 && chars.Count > MinCodons * CodonLength)
            {
                chars.RemoveAt(position);
            }
            else
            {
                chars.Insert(position, HexDigits[random.Next(16)]);
            }

            changes++;
        }

        return new string([.. chars]);
    }

    private static float Median(List<float> values, float fallback)
    {
        if (values.Count == 0)
            return fallback;
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2f
            : values[middle];
    }

    private static int Nibble(char character)
    {
        var upper = char.ToUpperInvariant(character);
        return upper switch
        {
            >= '0' and <= '9' => upper - '0',
            >= 'A' and <= 'F' => upper - 'A' + 10,
            _ => throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"'{character}' is not a hex digit.")),
        };
    }
}
