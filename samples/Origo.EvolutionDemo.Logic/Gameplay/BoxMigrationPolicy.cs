using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Result of checking a creature's expressed traits against a destination box.</summary>
public sealed record BoxMigrationValidation(bool Allowed, IReadOnlyList<string> Rejections);

/// <summary>Applies the destination box's content profile to a migrant's expressed traits.</summary>
public static class BoxMigrationPolicy
{
    /// <summary>Checks every expressed organ, action, and sensor against the destination gate.</summary>
    /// <param name="genome">Migrating genome.</param>
    /// <param name="destinationGate">Destination box content gate.</param>
    public static BoxMigrationValidation Validate(GenomeV4 genome, GenomeContentGate destinationGate)
    {
        ArgumentNullException.ThrowIfNull(genome);
        ArgumentNullException.ThrowIfNull(destinationGate);
        var phenotype = GenomeV4Compiler.Compile(genome, ExpressionContextV4.Neutral);
        var rejections = new List<string>();
        foreach (var organ in phenotype.Body.Organs.Select(static organ => organ.Kind).Distinct())
            if (!destinationGate.IsOrganAllowed(organ))
                rejections.Add($"organ:{organ}");
        foreach (var action in phenotype.ActionPorts.Select(static port => (ActionKindV4)port).Distinct())
            if (!destinationGate.IsActionAllowed(action))
                rejections.Add($"action:{action}");
        foreach (var sensor in phenotype.SensorPorts.Select(static port => (SensorKindV4)port).Distinct())
            if (!destinationGate.IsSensorAllowed(sensor))
                rejections.Add($"sensor:{sensor}");
        return new BoxMigrationValidation(rejections.Count == 0, rejections);
    }
}
