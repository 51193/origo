using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Persists idle progression on the SND world controller.</summary>
internal static class GameplayStateSync
{
    public static void Save(ISndEntity controller, GameplayRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(runtime);
        controller.SetData(DemoKeys.GameplayPoints,
            runtime.Progression.Points.ToString(CultureInfo.InvariantCulture));
        controller.SetData(DemoKeys.GameplayUnlocked,
            string.Join(';', runtime.Progression.UnlockedIds.OrderBy(static id => id, StringComparer.Ordinal)));
        controller.SetData(DemoKeys.GameplayMilestones,
            string.Join(';', runtime.Progression.CompletedMilestones.OrderBy(static id => id, StringComparer.Ordinal)));
        controller.SetData(DemoKeys.GameplayLifetimeSeconds, (float)runtime.Progression.LifetimeSeconds);
        GameplayProfileStore.Save(runtime.Progression);
    }
}
