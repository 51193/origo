using System;
using System.Numerics;

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
    bool SpikeOrgan,
    bool RhythmOrgan,
    bool VisionOrgan,
    bool CreatureSenseOrgan,
    bool SpikeBatteryOrgan,
    int SpikeCount,
    float SpikeSpreadRadians,
    float SpikeJitterRadians,
    bool HardDigestOrgan,
    bool ToxinResistanceOrgan,
    bool CarrionSenseOrgan)
{
}
