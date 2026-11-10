using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>Stable semantic sensor slots exposed by the demo runtime.</summary>
public enum SensorId
{
    FoodAhead = 0,
    FoodLeft = 1,
    FoodRight = 2,
    CreatureAhead = 3,
    WallAhead = 4,
    Energy = 5,
    Sin = 6,
    Cos = 7,
    FutureA = 8,
    FutureB = 9,
    FutureC = 10,
    FutureD = 11,
}

/// <summary>Stable semantic output slots exposed by the demo runtime.</summary>
public enum OutputId
{
    Move = 0,
    Turn = 1,
    Spike = 2,
    Aim = 3,
}

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
    int HiddenLayers,
    int HiddenWidth1,
    int HiddenWidth2,
    int HiddenWidth3,
    int Pattern,
    bool SpikeBatteryOrgan,
    int SpikeCount,
    float SpikeSpreadRadians,
    float SpikeJitterRadians,
    bool HardDigestOrgan,
    bool ToxinResistanceOrgan)
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
        OutputId.Spike or OutputId.Aim => SpikeOrgan,
        _ => false,
    };

    public int ActiveSensorCount =>
        (VisionOrgan ? 3 : 0) + (CreatureSenseOrgan ? 1 : 0) + 2 + (RhythmOrgan ? 2 : 0);

    public int ActiveOutputCount => SpikeOrgan ? 4 : 2;
}

/// <summary>Forward-evaluated result of one runtime neural graph.</summary>
public readonly record struct BrainOutput(
    float MoveDrive,
    float TurnDrive,
    float SpikeDrive,
    float AimDrive);
