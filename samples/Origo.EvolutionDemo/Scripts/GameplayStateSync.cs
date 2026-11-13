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
    public static void Load(ISndEntity controller, GameplayRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(runtime);
        var points = controller.TryGetData<string>(DemoKeys.GameplayPoints);
        if (points.found && long.TryParse(points.value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsedPoints))
            runtime.Progression.Points = parsedPoints;
        var unlocked = controller.TryGetData<string>(DemoKeys.GameplayUnlocked);
        if (unlocked.found && unlocked.value is not null)
        {
            runtime.Progression.UnlockedIds.Clear();
            foreach (var id in unlocked.value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                runtime.Progression.UnlockedIds.Add(id);
        }

        var milestones = controller.TryGetData<string>(DemoKeys.GameplayMilestones);
        if (milestones.found && milestones.value is not null)
        {
            runtime.Progression.CompletedMilestones.Clear();
            foreach (var id in milestones.value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                runtime.Progression.CompletedMilestones.Add(id);
        }

        var lifetime = controller.TryGetData<float>(DemoKeys.GameplayLifetimeSeconds);
        if (lifetime.found)
            runtime.Progression.LifetimeSeconds = lifetime.value;
    }

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
    }
}
