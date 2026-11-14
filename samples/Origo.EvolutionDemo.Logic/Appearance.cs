using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>A plain RGB color in sRGB space; Godot conversion stays in the presentation layer.</summary>
public readonly record struct AppearanceColor(float R, float G, float B)
{
    /// <summary>Returns a CSS-style hexadecimal representation.</summary>
    public string ToHex() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"#{Math.Clamp((int)MathF.Round(R * 255f), 0, 255):X2}{Math.Clamp((int)MathF.Round(G * 255f), 0, 255):X2}{Math.Clamp((int)MathF.Round(B * 255f), 0, 255):X2}");
}

/// <summary>Body pattern families rendered by the Godot view.</summary>
public enum CreaturePattern
{
    /// <summary>A plain body with a subtle rim.</summary>
    Plain = 0,

    /// <summary>A single inner ring.</summary>
    Ring = 1,

    /// <summary>Two concentric inner rings.</summary>
    DoubleRing = 2,

    /// <summary>Parallel stripes.</summary>
    Stripes = 3,

    /// <summary>Scattered spots.</summary>
    Spots = 4,

    /// <summary>Rays from the center.</summary>
    Rays = 5,

    /// <summary>A short spiral.</summary>
    Spiral = 6,

    /// <summary>A chevron/wedge mark.</summary>
    Chevron = 7,

    /// <summary>Segmented rim bands.</summary>
    Segments = 8,

    /// <summary>A central cross.</summary>
    Cross = 9,

    /// <summary>An outer halo ring.</summary>
    Halo = 10,

    /// <summary>Three dots arranged around the body.</summary>
    TriDots = 11,
}

/// <summary>
///     The genome-derived visual identity of a creature. Body color, accent
///     color, pattern family, and pattern parameters all come from the raw
///     genome string. Body/accent RGB are direct bytes folded from genome
///     segments (no theme-palette remap); similar genomes stay related.
/// </summary>
public sealed record CreatureAppearance(
    AppearanceColor Body,
    AppearanceColor Accent,
    CreaturePattern Pattern,
    int ThemeIndex,
    string ThemeName,
    float PatternAngle,
    float PatternScale,
    int PatternFrequency)
{
    /// <summary>Number of pattern families rendered by the demo.</summary>
    public const int PatternCount = 12;

    /// <summary>Number of named color themes.</summary>
    public const int ThemeCount = 8;

    /// <summary>Human-readable pattern name for the inspector/dashboard.</summary>
    public string PatternName => Pattern.ToString();
}

/// <summary>
///     Deterministic, genome-length-independent appearance derivation. Features
///     are weighted scans of normalized positions and nibble values; no fixed
///     offsets or length constants are used.
/// </summary>
public static class GenomeAppearance
{
    private const float Tau = MathF.PI * 2f;

    /// <summary>Derives an appearance from a raw genome string.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="genome" /> is null.</exception>
    public static CreatureAppearance Derive(string genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        // Body and accent colors are direct RGB bytes folded out of two
        // different raw-genome segments. There is no HSL/theme-palette remap
        // for the body: the six nibbles of each segment are the 0-255 channels.
        var length = Math.Max(1, genome.Length);
        var bodyStart = ((length * 37) / 100) % length;
        var accentStart = (bodyStart + Math.Max(3, length / 3)) % length;
        var body = DirectSegmentColor(genome, bodyStart);
        var accent = DirectSegmentColor(genome, accentStart);

        var rawHue = (Scan(genome, 1, 0.37f) * 1.7f)
            + (Scan(genome, 2, 1.91f) * 1.1f)
            + (Scan(genome, 3, 4.13f) * 0.6f);
        var theme = (int)(Fraction(((rawHue - 0.5f) * 3.1f) + 0.41f)
            * CreatureAppearance.ThemeCount) % CreatureAppearance.ThemeCount;
        var pattern = (CreaturePattern)((int)(Fraction(
            (Scan(genome, 3, 2.19f) * 5.7f)
            + (Scan(genome, 6, 1.07f) * 3.1f)
            + (Scan(genome, 7, 4.43f) * 2.3f)) * CreatureAppearance.PatternCount) % CreatureAppearance.PatternCount);
        var patternAngle = Scan(genome, 7, 0.43f) * Tau;
        var patternScale = 0.48f + (Scan(genome, 5, 2.57f) * 0.34f);
        var patternFrequency = 2 + (int)(Scan(genome, 4, 1.19f) * 4.99f);

        return new CreatureAppearance(
            body,
            accent,
            pattern,
            theme,
            ThemeNameFor(theme),
            patternAngle,
            patternScale,
            patternFrequency);
    }

    private static AppearanceColor DirectSegmentColor(string genome, int start)
    {
        if (genome.Length == 0)
            return new AppearanceColor(0.5f, 0.5f, 0.5f);

        var length = genome.Length;
        var r = (Nibble(genome[start % length]) * 16) + Nibble(genome[(start + 1) % length]);
        var g = (Nibble(genome[(start + 2) % length]) * 16) + Nibble(genome[(start + 3) % length]);
        var b = (Nibble(genome[(start + 4) % length]) * 16) + Nibble(genome[(start + 5) % length]);
        return new AppearanceColor(r / 255f, g / 255f, b / 255f);
    }

    private static int Nibble(char character)
    {
        var upper = char.ToUpperInvariant(character);
        if (upper is >= '0' and <= '9')
            return upper - '0';
        if (upper is >= 'A' and <= 'F')
            return upper - 'A' + 10;
        return upper % 16;
    }

    /// <summary>Returns a representative theme color for dashboards/legends.</summary>
    public static AppearanceColor ThemeColor(int themeIndex)
    {
        var index = (int)MathF.Abs(themeIndex) % CreatureAppearance.ThemeCount;
        return FromHsv(index / (float)CreatureAppearance.ThemeCount, 0.72f, 0.95f);
    }

    /// <summary>Returns a human-readable theme name.</summary>
    public static string ThemeNameFor(int themeIndex)
    {
        var index = (int)MathF.Abs(themeIndex) % CreatureAppearance.ThemeCount;
        return index switch
        {
            0 => "Ember",
            1 => "Amber",
            2 => "Moss",
            3 => "Reef",
            4 => "Tide",
            5 => "Dusk",
            6 => "Bloom",
            _ => "Frost",
        };
    }

    private static float Scan(string genome, int frequency, float phase)
    {
        if (genome.Length == 0)
            return 0.5f;

        var weighted = 0d;
        var weight = 0d;
        for (var i = 0; i < genome.Length; i++)
        {
            var t = (i + 0.5f) / genome.Length;
            var value = ValueOf(genome[i]);
            var localWeight = 0.5 + (0.5 * Math.Sin((Tau * frequency * t) + phase));
            weighted += value * localWeight;
            weight += localWeight;
        }

        return weight > 0d ? (float)(weighted / weight) : 0.5f;
    }

    private static float ValueOf(char character)
    {
        var upper = char.ToUpperInvariant(character);
        if (upper is >= '0' and <= '9')
            return (upper - '0') / 15f;
        if (upper is >= 'A' and <= 'F')
            return (upper - 'A' + 10) / 15f;
        return (upper % 251) / 250f;
    }

    private static float Fraction(float value) => value - MathF.Floor(value);

    private static AppearanceColor FromHsv(float hue, float saturation, float value)
    {
        var h = Fraction(hue) * 6f;
        var s = Math.Clamp(saturation, 0f, 1f);
        var v = Math.Clamp(value, 0f, 1f);
        var c = v * s;
        var x = c * (1f - MathF.Abs((h % 2f) - 1f));
        var m = v - c;
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x),
        };
        return new AppearanceColor(
            Math.Clamp(r + m, 0f, 1f),
            Math.Clamp(g + m, 0f, 1f),
            Math.Clamp(b + m, 0f, 1f));
    }
}
