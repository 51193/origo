using System;
using System.Numerics;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Logic;

/// <summary>A point a sensor can evaluate, with its entity id.</summary>
public readonly record struct SensorPoint(int Id, Vector2 Position);

/// <summary>
///     Runtime-facing phenotype. It is compiled from Genome v4; the fields are
///     the current demo's stable presentation surface, not a genome encoding.
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
    int OrganMask,
    int SpikeCount,
    float SpikeSpreadRadians,
    float SpikeJitterRadians)
{
    /// <summary>True when the expressed body contains the spike organ.</summary>
    public bool SpikeOrgan => HasOrgan(DemoOrganMaskV4.Spike);

    /// <summary>True when the body contains the vision organ.</summary>
    public bool VisionOrgan => HasOrgan(DemoOrganMaskV4.Vision);

    /// <summary>True when the body contains the rhythm organ.</summary>
    public bool RhythmOrgan => HasOrgan(DemoOrganMaskV4.Rhythm);

    /// <summary>True when the body contains the creature sensor organ.</summary>
    public bool CreatureSenseOrgan => HasOrgan(DemoOrganMaskV4.CreatureSense);

    /// <summary>True when the body contains the spike battery organ.</summary>
    public bool SpikeBatteryOrgan => HasOrgan(DemoOrganMaskV4.SpikeBattery);

    /// <summary>True when the body contains the hard-digest organ.</summary>
    public bool HardDigestOrgan => HasOrgan(DemoOrganMaskV4.HardDigest);

    /// <summary>True when the body contains toxin resistance.</summary>
    public bool ToxinResistanceOrgan => HasOrgan(DemoOrganMaskV4.ToxinResistance);

    /// <summary>True when the body contains the carrion sensor.</summary>
    public bool CarrionSenseOrgan => HasOrgan(DemoOrganMaskV4.CarrionSense);

    /// <summary>True when the body contains the sprint organ.</summary>
    public bool SprintOrgan => HasOrgan(DemoOrganMaskV4.Sprint);

    private bool HasOrgan(DemoOrganMaskV4 organ) => (OrganMask & (int)organ) != 0;
}
