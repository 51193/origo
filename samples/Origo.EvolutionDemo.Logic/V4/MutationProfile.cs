using System;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>
///     Mutation operator mix derived from the demo's body, brain, and body-length
///     mutation rates. The rates scale the typed V4 operator weights, so the New
///     Pond presets and Advanced Settings change which mutations offspring
///     receive instead of being discarded by the mutator.
/// </summary>
public sealed record MutationProfile(GenomeMutationSettingsV4 Settings, int Steps)
{
    private static readonly DemoSettings Baseline = DemoSettings.Defaults();
    private static readonly GenomeMutationSettingsV4 BaseWeights = new();

    /// <summary>
    ///     Builds the operator mix for the given settings. The returned profile
    ///     has zero steps; callers set the per-birth step count with
    ///     <see cref="WithSteps" /> so the RNG draw stays at the call site.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings" /> is null.</exception>
    public static MutationProfile FromSettings(DemoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var bodyScale = ScaleFor(settings.BodyMutationRate, Baseline.BodyMutationRate);
        var brainScale = ScaleFor(settings.BrainMutationRate, Baseline.BrainMutationRate);
        var lengthScale = ScaleFor(settings.BodyLengthMutationRate, Baseline.BodyLengthMutationRate);
        return new MutationProfile(
            Normalize(new GenomeMutationSettingsV4
            {
                Expression = Scaled(BaseWeights.Expression, bodyScale),
                CopyCount = Scaled(BaseWeights.CopyCount, bodyScale),
                Payload = Scaled(BaseWeights.Payload, bodyScale),
                AddOrgan = Scaled(BaseWeights.AddOrgan, bodyScale),
                AddRegulation = Scaled(BaseWeights.AddRegulation, bodyScale),
                Duplicate = Scaled(BaseWeights.Duplicate, lengthScale),
                Delete = Scaled(BaseWeights.Delete, lengthScale),
                AddSegment = Scaled(BaseWeights.AddSegment, lengthScale),
                AddNeuron = Scaled(BaseWeights.AddNeuron, brainScale),
                AddSynapse = Scaled(BaseWeights.AddSynapse, brainScale),
                AddReflex = Scaled(BaseWeights.AddReflex, brainScale),
            }),
            0);
    }

    /// <summary>
    ///     Builds an intentionally stronger mix for overgrowth descendants: the
    ///     operator weights are amplified by the overgrowth mutation rate and the
    ///     step count grows to 3-6 typed operators.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when a parameter is null.</exception>
    public static MutationProfile ForOvergrowth(DemoSettings settings, Random random)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        var baseProfile = FromSettings(settings);
        var amplify = 1d + (Math.Clamp(settings.OvergrowthMutationRate, 0d, 1d) * 4d);
        var settingsBase = baseProfile.Settings;
        var boosted = new GenomeMutationSettingsV4
        {
            Expression = settingsBase.Expression,
            CopyCount = settingsBase.CopyCount,
            Duplicate = settingsBase.Duplicate * amplify,
            Delete = settingsBase.Delete * amplify,
            Payload = settingsBase.Payload * amplify,
            AddNeuron = settingsBase.AddNeuron,
            AddSynapse = settingsBase.AddSynapse,
            AddReflex = settingsBase.AddReflex,
            AddOrgan = settingsBase.AddOrgan * amplify,
            AddRegulation = settingsBase.AddRegulation,
            AddSegment = settingsBase.AddSegment * amplify,
        };
        return new MutationProfile(Normalize(boosted), 3 + random.Next(4));
    }

    /// <summary>Returns a copy with the given number of typed mutation steps.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="steps" /> is negative.</exception>
    public MutationProfile WithSteps(int steps)
    {
        if (steps < 0)
            throw new ArgumentOutOfRangeException(nameof(steps), "Mutation steps must be zero or positive.");
        return this with { Steps = steps };
    }

    private static double ScaleFor(double rate, double baseline) =>
        baseline <= 0d ? 1d : System.Math.Max(0d, rate) / baseline;

    private static double Scaled(double weight, double scale) =>
        System.Math.Clamp(weight * scale, 0d, 1d);

    private static GenomeMutationSettingsV4 ScaleRaw(GenomeMutationSettingsV4 settings, double scale) =>
        new()
        {
            Expression = Scaled(settings.Expression, scale),
            CopyCount = Scaled(settings.CopyCount, scale),
            Duplicate = Scaled(settings.Duplicate, scale),
            Delete = Scaled(settings.Delete, scale),
            Payload = Scaled(settings.Payload, scale),
            AddNeuron = Scaled(settings.AddNeuron, scale),
            AddSynapse = Scaled(settings.AddSynapse, scale),
            AddReflex = Scaled(settings.AddReflex, scale),
            AddOrgan = Scaled(settings.AddOrgan, scale),
            AddRegulation = Scaled(settings.AddRegulation, scale),
            AddSegment = Scaled(settings.AddSegment, scale),
        };

    private static GenomeMutationSettingsV4 Normalize(GenomeMutationSettingsV4 settings)
    {
        var total = settings.Expression + settings.CopyCount + settings.Duplicate + settings.Delete
            + settings.Payload + settings.AddNeuron + settings.AddSynapse + settings.AddReflex
            + settings.AddOrgan + settings.AddRegulation + settings.AddSegment;
        if (total <= 0d)
            return settings;
        return ScaleRaw(settings, 1d / total);
    }
}
