using System;
using System.Linq;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Evaluates expressed V4 action ports without a fixed action struct.</summary>
public static class DemoActionRuntime
{
    /// <summary>Maximum dense action-interface id supported by the runtime buffer.</summary>
    public const int MaxInterfaceId = 63;

    /// <summary>Fills action values only for ports expressed by the phenotype.</summary>
    public static void Evaluate(
        PhenotypeV4 phenotype,
        CompiledBrainV4 brain,
        ReadOnlySpan<float> sensorValues,
        Span<float> actions)
    {
        ArgumentNullException.ThrowIfNull(phenotype);
        ArgumentNullException.ThrowIfNull(brain);
        if (actions.Length <= MaxInterfaceId)
            throw new ArgumentException($"Action buffer must be larger than {MaxInterfaceId}.", nameof(actions));
        actions.Clear();
        brain.Evaluate(sensorValues, actions);
        var expressed = phenotype.ActionPorts;
        for (var id = 0; id < actions.Length; id++)
        {
            if (expressed.Contains((ushort)id))
                continue;
            actions[id] = 0f;
        }
    }
}
