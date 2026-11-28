using System;
using Origo.Core.Abstractions.Entity;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Outcome of one creature migration between ecology boxes.</summary>
public sealed record BoxMigrationResult(
    bool Success,
    string? Reason,
    string? MigrantName,
    string SourceBoxId,
    string DestinationBoxId);

/// <summary>Moves a creature without recompiling or stripping its stable traits.</summary>
public static class BoxMigration
{
    /// <summary>Migrates a living creature to another box when the destination profile permits every expressed trait.</summary>
    /// <param name="host">Owner of both boxes.</param>
    /// <param name="sourceBoxId">Source box id.</param>
    /// <param name="creatureName">Source creature entity name.</param>
    /// <param name="destinationBoxId">Destination box id.</param>
    public static BoxMigrationResult Migrate(
        EvolutionGameSession host,
        string sourceBoxId,
        string creatureName,
        string destinationBoxId)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceBoxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(creatureName);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationBoxId);
        if (string.Equals(sourceBoxId, destinationBoxId, StringComparison.Ordinal))
            return Reject(host, sourceBoxId, destinationBoxId, "source and destination boxes are the same");

        var source = host.TryGetBox(sourceBoxId)
            ?? throw new InvalidOperationException($"Source box '{sourceBoxId}' is not registered.");
        var destination = host.TryGetBox(destinationBoxId)
            ?? throw new InvalidOperationException($"Destination box '{destinationBoxId}' is not registered.");
        var sourceEntity = source.Session.FindByName(creatureName);
        if (sourceEntity is null)
            return Reject(host, sourceBoxId, destinationBoxId, "source creature is missing");
        if (sourceEntity.IsPendingKill)
            return Reject(host, sourceBoxId, destinationBoxId, "source creature is already dying");
        if (!string.Equals(ReadString(sourceEntity, DemoKeys.Kind, string.Empty), DemoKeys.CreatureKind,
                StringComparison.Ordinal))
            return Reject(host, sourceBoxId, destinationBoxId, "source entity is not a creature");
        var serialized = ReadString(sourceEntity, DemoKeys.BodyGenome, string.Empty);
        if (!CreatureGenome.TryDecode(serialized, out var genome) || genome is null)
            return Reject(host, sourceBoxId, destinationBoxId, "creature genome is invalid");
        var validation = BoxMigrationPolicy.Validate(genome.Genome, destination.Runtime.GenomeGate);
        if (!validation.Allowed)
            return Reject(host, sourceBoxId, destinationBoxId,
                "destination profile rejects " + string.Join(", ", validation.Rejections));

        var targetController = destination.Controller
            ?? throw new InvalidOperationException($"Destination box '{destinationBoxId}' has no controller.");
        if (CountCreatures(destination.Session) >= Origo.EvolutionDemo.Logic.Tuning.PopulationCap)
            return Reject(host, sourceBoxId, destinationBoxId, "destination box is at population cap");
        var targetId = ReadInt(targetController, DemoKeys.NextCreatureId, 1);
        var migrantName = EvolutionEntityFactory.CreatureName(targetId);
        var phenotype = genome.Phenotype;
        var migrant = destination.Session.Spawn(EvolutionEntityFactory.Creature(
            migrantName,
            genome,
            phenotype,
            0f,
            0f,
            ReadFloat(sourceEntity, DemoKeys.Heading, 0f),
            MathF.Max(0f, ReadFloat(sourceEntity, DemoKeys.Energy, 0f)),
            ReadInt(sourceEntity, DemoKeys.Generation, 0),
            ReadInt(sourceEntity, DemoKeys.Lineage, 0),
            ReadInt(sourceEntity, DemoKeys.RootLineage, 0),
            "migration:" + sourceBoxId,
            0f));
        migrant.SetData(DemoKeys.Age, ReadFloat(sourceEntity, DemoKeys.Age, 0f));
        migrant.SetData(DemoKeys.Children, ReadInt(sourceEntity, DemoKeys.Children, 0));
        migrant.SetData(DemoKeys.Kills, ReadInt(sourceEntity, DemoKeys.Kills, 0));
        migrant.SetData(DemoKeys.CreatureCorpseMeals, ReadInt(sourceEntity, DemoKeys.CreatureCorpseMeals, 0));
        migrant.SetData(DemoKeys.CreatureSoftMeals, ReadInt(sourceEntity, DemoKeys.CreatureSoftMeals, 0));
        migrant.SetData(DemoKeys.CreatureToughMeals, ReadInt(sourceEntity, DemoKeys.CreatureToughMeals, 0));
        migrant.SetData(DemoKeys.CreatureToxicMeals, ReadInt(sourceEntity, DemoKeys.CreatureToxicMeals, 0));
        migrant.SetData(DemoKeys.ReproCooldown, ReadFloat(sourceEntity, DemoKeys.ReproCooldown, 0f));
        migrant.SetData(DemoKeys.RootOriginBox, ReadString(sourceEntity, DemoKeys.RootOriginBox, sourceBoxId));
        targetController.SetData(DemoKeys.NextCreatureId, targetId + 1);
        targetController.SetData(DemoKeys.MigrationsIn,
            ReadInt(targetController, DemoKeys.MigrationsIn, 0) + 1);
        var sourceController = source.Controller;
        if (sourceController is not null)
            sourceController.SetData(DemoKeys.MigrationsOut,
                ReadInt(sourceController, DemoKeys.MigrationsOut, 0) + 1);
        source.Session.RequestKillEntity(sourceEntity.Name);
        return new BoxMigrationResult(true, null, migrantName, sourceBoxId, destinationBoxId);
    }

    private static int CountCreatures(Origo.Core.Abstractions.Lifecycle.ISessionRun session)
    {
        var count = 0;
        foreach (var entity in session.GetEntities())
        {
            if (entity.IsPendingKill)
                continue;
            if (entity.TryGetData<string>(DemoKeys.Kind) is { found: true, value: DemoKeys.CreatureKind })
                count++;
        }

        return count;
    }

    private static BoxMigrationResult Reject(
        EvolutionGameSession host,
        string sourceBoxId,
        string destinationBoxId,
        string reason)
    {
        if (host.TryGetBox(sourceBoxId)?.Controller is { } controller)
            controller.SetData(DemoKeys.MigrationRejects,
                ReadInt(controller, DemoKeys.MigrationRejects, 0) + 1);
        return new BoxMigrationResult(false, reason, null, sourceBoxId, destinationBoxId);
    }

    private static float ReadFloat(ISndEntity entity, string key, float fallback) =>
        entity.TryGetData<float>(key) is { found: true } result ? result.value : fallback;

    private static int ReadInt(ISndEntity entity, string key, int fallback) =>
        entity.TryGetData<int>(key) is { found: true } result ? result.value : fallback;

    private static string ReadString(ISndEntity entity, string key, string fallback) =>
        entity.TryGetData<string>(key) is { found: true, value: { } result } ? result : fallback;
}
