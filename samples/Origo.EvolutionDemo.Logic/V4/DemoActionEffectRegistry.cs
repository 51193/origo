using System;
using System.Collections.Generic;
using System.Numerics;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Mutable simulation state consumed by action effects.</summary>
public sealed class DemoActionEffectState
{
    public Vector2 Position { get; set; }
    public float Heading { get; set; }
    public float MoveSpeed { get; set; }
    public float TurnSpeed { get; set; }
    public float DeltaSeconds { get; set; }
    public float MoveSpeedMultiplier { get; set; } = 1f;
}

/// <summary>
///     Registry for action effects that mutate the per-tick creature state.
///     Move/Turn are registered here; more complex world effects can be added
///     without adding another fixed action branch to the simulation loop.
/// </summary>
public static class DemoActionEffectRegistry
{
    private static readonly Dictionary<ushort, Action<DemoActionEffectState, float>> Effects = [];

    static DemoActionEffectRegistry()
    {
        Register(ActionKindV4.Move, static (state, value) =>
        {
            if (value <= Tuning.DriveThreshold)
                return;
            var forward = new Vector2(MathF.Cos(state.Heading), MathF.Sin(state.Heading));
            state.Position += forward * (state.MoveSpeed * state.MoveSpeedMultiplier * value * state.DeltaSeconds);
        });
        Register(ActionKindV4.Turn, static (state, value) =>
        {
            state.Heading += value * state.TurnSpeed * state.DeltaSeconds;
        });
        Register(ActionKindV4.Spike, static (_, _) => { });
        Register(ActionKindV4.Aim, static (_, _) => { });
        Register(ActionKindV4.Sprint, static (state, value) =>
        {
            if (value > 0.4f)
                state.MoveSpeedMultiplier = 1.6f;
        });
    }

    /// <summary>Registers or replaces one action effect.</summary>
    public static void Register(ActionKindV4 action, Action<DemoActionEffectState, float> effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        Effects[(ushort)action] = effect;
    }

    /// <summary>Applies only the actions expressed by this phenotype.</summary>
    public static void Apply(
        PhenotypeV4 phenotype,
        ReadOnlySpan<float> actions,
        DemoActionEffectState state)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        ArgumentNullException.ThrowIfNull(state);
        foreach (var port in phenotype.ActionPorts)
        {
            if (port < actions.Length && Effects.TryGetValue(port, out var effect))
                effect(state, actions[port]);
        }
    }
}
