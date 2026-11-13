using System;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>
///     Runtime hook that lets gameplay content gate which organ/action/sensor
///     genes may be added to founders or mutations. Defaults are open so the
///     Godot-free genome tests and tools do not need a gameplay host.
/// </summary>
public static class GenomeContentGate
{
    public static Func<OrganKindV4, bool> OrganFilter { get; set; } = static _ => true;
    public static Func<ActionKindV4, bool> ActionFilter { get; set; } = static _ => true;
    public static Func<SensorKindV4, bool> SensorFilter { get; set; } = static _ => true;

    public static bool IsOrganAllowed(OrganKindV4 organ) => OrganFilter(organ);
    public static bool IsActionAllowed(ActionKindV4 action) => ActionFilter(action);
    public static bool IsSensorAllowed(SensorKindV4 sensor) => SensorFilter(sensor);
}
