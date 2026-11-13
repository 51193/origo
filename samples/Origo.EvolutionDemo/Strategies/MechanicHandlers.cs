using System;
using System.Collections.Generic;
using System.Globalization;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;
using NumericsVector2 = System.Numerics.Vector2;

namespace Origo.EvolutionDemo;

/// <summary>Typed context for one creature death event.</summary>
internal sealed class DeathHandlerContext
{
    public required ISndEntity Controller { get; init; }
    public required CreatureSnapshot Creature { get; init; }
    public required NumericsVector2 Position { get; init; }
    public required Random Rng { get; init; }
    public required List<SndMetaData> Mutations { get; init; }
    public int FoodAlive { get; set; }
    public int PendingFood { get; set; }
    public int NextFoodId { get; set; }
    public int NextCorpseId { get; set; }
    public int CorpsesCreated { get; set; }
}

internal sealed class InstantFoodDeathHandler : IGameplayHandler<DeathHandlerContext>
{
    public string Id => "instant_food";

    public void Execute(DeathHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var foodCount = Math.Min(
            Tuning.DeathFoodCount,
            Math.Max(0, Tuning.FoodCap - (context.FoodAlive + context.PendingFood)));
        for (var bite = 0; bite < foodCount; bite++)
        {
            var foodPosition = WorldSimulationStrategy.ClampToPond(
                context.Position + WorldSimulationStrategy.RandomOffset(context.Rng),
                context.Creature.FoodPickupRadius);
            context.Mutations.Add(EvolutionEntityFactory.Food(
                EvolutionEntityFactory.FoodName(context.NextFoodId++),
                foodPosition.X,
                foodPosition.Y));
            context.FoodAlive++;
            context.PendingFood++;
        }
    }
}

internal sealed class CorpsePulseDeathHandler : IGameplayHandler<DeathHandlerContext>
{
    public string Id => "corpse_pulse";

    public void Execute(DeathHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Tuning.DeathFoodCount <= 0)
            return;
        context.Mutations.Add(EvolutionEntityFactory.Corpse(
            EvolutionEntityFactory.CorpseName(context.NextCorpseId++),
            context.Position.X,
            context.Position.Y,
            Tuning.DeathFoodCount,
            Tuning.CarcassFoodIntervalSeconds));
        context.CorpsesCreated++;
    }
}

/// <summary>Typed context for a food-source spawn-policy decision.</summary>
internal sealed class SpawnPolicyContext
{
    public required ISndEntity Controller { get; init; }
    public required ISndEntity Source { get; init; }
    public required Random Rng { get; init; }
    public required List<SndMetaData> Metas { get; init; }
    public required string HandlerId { get; init; }
    public int NextCreatureId { get; set; }
    public int NextLineageId { get; set; }
}

internal sealed class BaseSpawnPolicyHandler : IGameplayHandler<SpawnPolicyContext>
{
    public string Id => "base_spawn";

    public void Execute(SpawnPolicyContext context)
    {
    }
}

internal sealed class FoodEnrichmentSpawnHandler : IGameplayHandler<SpawnPolicyContext>
{
    public string Id => "food_enrichment_spawn";

    public void Execute(SpawnPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = context.Source.OwningSession;
        if (CountCreatures(session) >= Tuning.PopulationCap || context.Rng.NextDouble() >= 0.08d)
            return;
        var founder = CreatureGenome.RandomFounder(context.Rng);
        var position = new NumericsVector2(
            ReadFloat(context.Source, DemoKeys.X, 0f),
            ReadFloat(context.Source, DemoKeys.Y, 0f));
        context.Metas.Add(EvolutionEntityFactory.Creature(
            EvolutionEntityFactory.CreatureName(context.NextCreatureId++),
            founder,
            founder.Phenotype,
            position.X,
            position.Y,
            (float)(context.Rng.NextDouble() * MathF.Tau),
            MathF.Min(Tuning.InitialEnergy, founder.Phenotype.MaxEnergy),
            0,
            context.NextLineageId++,
            0f));
    }

    private static int CountCreatures(ISessionRun session)
    {
        var count = 0;
        foreach (var candidate in session.GetEntities())
        {
            if (candidate.IsPendingKill)
                continue;
            if (candidate.TryGetData<string>(DemoKeys.Kind) is { found: true } result
                && string.Equals(result.value, DemoKeys.CreatureKind, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static float ReadFloat(ISndEntity entity, string key, float fallback) =>
        entity.TryGetData<float>(key) is { found: true } result ? result.value : fallback;
}

/// <summary>Startup-built typed handler registries for the demo.</summary>
internal static class DemoMechanicHandlers
{
    public static GameplayHandlerRegistry<DeathHandlerContext> Death { get; } = CreateDeath();
    public static GameplayHandlerRegistry<SpawnPolicyContext> Spawn { get; } = CreateSpawn();

    public static void Validate(GameplayRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Death.Validate(runtime.Content, "death.handler");
        Spawn.Validate(runtime.Content, "spawn.policy");
    }

    private static GameplayHandlerRegistry<DeathHandlerContext> CreateDeath()
    {
        var registry = new GameplayHandlerRegistry<DeathHandlerContext>();
        registry.Register(new InstantFoodDeathHandler());
        registry.Register(new CorpsePulseDeathHandler());
        return registry;
    }

    private static GameplayHandlerRegistry<SpawnPolicyContext> CreateSpawn()
    {
        var registry = new GameplayHandlerRegistry<SpawnPolicyContext>();
        registry.Register(new BaseSpawnPolicyHandler());
        registry.Register(new FoodEnrichmentSpawnHandler());
        return registry;
    }
}
