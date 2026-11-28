using System;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>
///     Scoped gate that lets one gameplay box restrict which organ/action/sensor
///     genes may be added to founders or mutations. Each box owns its gate; no
///     process-global mutable filter exists.
/// </summary>
/// <param name="organ">Organ predicate.</param>
/// <param name="action">Action predicate.</param>
/// <param name="sensor">Sensor predicate.</param>
/// <exception cref="ArgumentNullException">A predicate is null.</exception>
public sealed class GenomeContentGate(
    Func<OrganKindV4, bool> organ,
    Func<ActionKindV4, bool> action,
    Func<SensorKindV4, bool> sensor)
{
    private readonly Func<OrganKindV4, bool> _organ =
        organ ?? throw new ArgumentNullException(nameof(organ));
    private readonly Func<ActionKindV4, bool> _action =
        action ?? throw new ArgumentNullException(nameof(action));
    private readonly Func<SensorKindV4, bool> _sensor =
        sensor ?? throw new ArgumentNullException(nameof(sensor));

    /// <summary>Gate that allows every interface; used by Godot-free genome tools.</summary>
    public static GenomeContentGate Open { get; } = new(
        static _ => true,
        static _ => true,
        static _ => true);

    public bool IsOrganAllowed(OrganKindV4 organ) => _organ(organ);
    public bool IsActionAllowed(ActionKindV4 action) => _action(action);
    public bool IsSensorAllowed(SensorKindV4 sensor) => _sensor(sensor);
}
