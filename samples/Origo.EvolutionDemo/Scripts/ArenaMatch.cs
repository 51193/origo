using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>One cohort's starting and surviving population in an arena match.</summary>
public sealed record ArenaCohortResult(string SourceBoxId, int Started, int Survived);

/// <summary>Final arena outcome.</summary>
public sealed record ArenaResult(
    IReadOnlyList<ArenaCohortResult> Cohorts,
    string? Winner,
    double ElapsedSeconds);

/// <summary>Runs a neutral, all-enabled ecology-box competition.</summary>
public sealed class ArenaMatch
{
    /// <summary>Box id used by the ephemeral arena session.</summary>
    public const string ArenaBoxId = "arena";

    private readonly Dictionary<string, int> _started = new(StringComparer.Ordinal);

    private ArenaMatch(EcologyBox arena, double durationSeconds)
    {
        Arena = arena;
        DurationSeconds = durationSeconds;
    }

    /// <summary>Arena box.</summary>
    public EcologyBox Arena { get; }

    /// <summary>Match duration in simulation seconds.</summary>
    public double DurationSeconds { get; }

    /// <summary>Creates a neutral arena and imports cohorts without removing their source creatures.</summary>
    /// <param name="host">Owner of all ecology boxes.</param>
    /// <param name="sourceBoxIds">Competing box ids.</param>
    /// <param name="perCohort">Creatures imported from each box.</param>
    /// <param name="seed">Arena simulation seed.</param>
    /// <param name="durationSeconds">Match duration in simulation seconds.</param>
    public static ArenaMatch Start(
        EvolutionGameSession host,
        IReadOnlyList<string> sourceBoxIds,
        int perCohort,
        int seed,
        double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(sourceBoxIds);
        if (sourceBoxIds.Count < 2)
            throw new ArgumentException("An arena needs at least two source boxes.", nameof(sourceBoxIds));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perCohort);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);

        host.RemoveBox(ArenaBoxId);
        var arena = host.CreateBackgroundBox(ArenaBoxId, host.DefaultProfile, seed);
        var controller = arena.Controller
            ?? throw new InvalidOperationException("Arena box has no controller.");
        var settings = DemoSettings.Defaults();
        settings.PondRadius = 700f;
        settings.InitialPopulation = 1;
        settings.PopulationCap = Math.Max(16, sourceBoxIds.Count * perCohort * 2);
        settings.InitialFood = 400;
        settings.FoodCap = 2_000;
        settings.FoodPatchCount = 3;
        settings.FoodPatchRadius = 250f;
        settings.FoodPatchLocalCapacity = 120;
        settings.CollisionEnabled = true;
        settings.EarlyFoodBloomEnabled = false;
        settings.ReproductionEnergy = 10_000f;
        settings.ReproductionParentCost = 10_000f;
        settings.StartupFoodPerFounder = 0;
        controller.SetData(DemoKeys.ConfigJson, settings.ToJson());
        controller.SetData(DemoKeys.ArenaMode, true);
        controller.SetData(DemoKeys.Seed, seed);
        controller.SetData(DemoKeys.Phase, DemoKeys.PhaseStarting);
        controller.SetData(DemoKeys.Paused, false);

        var match = new ArenaMatch(arena, durationSeconds);
        foreach (var sourceBoxId in sourceBoxIds)
        {
            var source = host.TryGetBox(sourceBoxId)
                ?? throw new InvalidOperationException($"Arena source box '{sourceBoxId}' is missing.");
            var creatures = source.Session.GetEntities()
                .Where(static entity => !entity.IsPendingKill)
                .Where(static entity => entity.TryGetData<string>(DemoKeys.Kind) is { found: true, value: DemoKeys.CreatureKind })
                .OrderBy(static entity => entity.Name, StringComparer.Ordinal)
                .Take(perCohort)
                .ToArray();
            match._started[sourceBoxId] = creatures.Length;
            foreach (var creature in creatures)
            {
                var migrated = BoxMigration.Migrate(host, sourceBoxId, creature.Name, ArenaBoxId, removeSource: false);
                if (!migrated.Success || migrated.MigrantName is null)
                    throw new InvalidOperationException($"Arena import failed: {migrated.Reason}.");
                var migrant = arena.Session.FindByName(migrated.MigrantName)
                    ?? throw new InvalidOperationException("Arena migrant was not mounted.");
                migrant.SetData(DemoKeys.ArenaCohort, sourceBoxId);
            }
        }

        return match;
    }

    /// <summary>Reads the result once the arena reaches its duration or becomes extinct.</summary>
    public ArenaResult? Poll(EvolutionGameSession host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var controller = Arena.Controller
            ?? throw new InvalidOperationException("Arena controller disappeared.");
        var time = controller.TryGetData<float>(DemoKeys.Time) is { found: true } value ? value.value : 0f;
        var phase = controller.TryGetData<string>(DemoKeys.Phase) is { found: true } current
            ? current.value
            : DemoKeys.PhaseMenu;
        if (time < DurationSeconds && !string.Equals(phase, DemoKeys.PhaseExtinct, StringComparison.Ordinal))
            return null;

        var alive = Arena.Session.GetEntities()
            .Where(static entity => !entity.IsPendingKill)
            .Where(static entity => entity.TryGetData<string>(DemoKeys.Kind) is { found: true, value: DemoKeys.CreatureKind })
            .Select(static entity => entity.TryGetData<string>(DemoKeys.ArenaCohort) is { found: true, value: { } cohort }
                ? cohort
                : string.Empty)
            .Where(static cohort => cohort.Length > 0)
            .GroupBy(static cohort => cohort, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var cohorts = _started
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ArenaCohortResult(pair.Key, pair.Value, alive.GetValueOrDefault(pair.Key)))
            .ToArray();
        var winner = cohorts.OrderByDescending(static cohort => cohort.Survived)
            .ThenBy(static cohort => cohort.SourceBoxId, StringComparer.Ordinal)
            .FirstOrDefault() is { Survived: > 0 } top && cohorts.Count(cohort => cohort.Survived == top.Survived) == 1
            ? top.SourceBoxId
            : null;
        var result = new ArenaResult(cohorts, winner, time);
        host.RemoveBox(ArenaBoxId);
        return result;
    }
}
