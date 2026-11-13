using System;
using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Logic.Gameplay;

/// <summary>Default content module for the current demo mechanics.</summary>
public sealed class DefaultGameplayContentModule : IGameplayModule
{
    public string Id => "core";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public void Register(GameplayContentRegistry content, GameplayModuleServices services)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(services);

        AddContent(content, "gene.organ.spike", "organ", "unlock.core");
        AddContent(content, "gene.organ.vision", "organ", "unlock.core");
        AddContent(content, "gene.organ.rhythm", "organ", "unlock.core");
        AddContent(content, "gene.organ.creature_sense", "organ", "unlock.core");
        AddContent(content, "gene.organ.spike_battery", "organ", "unlock.core");
        AddContent(content, "gene.organ.hard_digest", "organ", "unlock.core");
        AddContent(content, "gene.organ.toxin_resistance", "organ", "unlock.core");
        AddContent(content, "gene.organ.carrion_sense", "organ", "unlock.core");
        AddContent(content, "gene.organ.sprint", "organ", "unlock.core");
        AddContent(content, "gene.action.move", "action", "unlock.core");
        AddContent(content, "gene.action.turn", "action", "unlock.core");
        AddContent(content, "gene.action.spike", "action", "unlock.core");
        AddContent(content, "gene.action.aim", "action", "unlock.core");
        AddContent(content, "gene.action.sprint", "action", "unlock.core");
        AddContent(content, "gene.sensor.food_ahead", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.food_left", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.food_right", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.wall_ahead", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.energy", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.creature_ahead", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.corpse_ahead", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.sin", "sensor", "unlock.core");
        AddContent(content, "gene.sensor.cos", "sensor", "unlock.core");
        AddContent(content, "mechanic.death.instant_food", "mechanic", "unlock.core");
        AddContent(content, "mechanic.death.corpse_pulse", "mechanic", "unlock.core");
        AddContent(content, "mechanic.spawn.base", "mechanic", "unlock.core");
        AddContent(content, "mechanic.spawn.enrichment", "mechanic", "unlock.spawn.enrichment");

        content.AddUnlock(new UnlockDefinition(
            "unlock.core", "Core evolution", "core", 0, [], [],
            []));
        content.AddUnlock(new UnlockDefinition(
            "unlock.spawn.enrichment", "Enrichment spawning", "mechanics", 5_000,
            ["unlock.core"], ["mechanic.spawn.enrichment"], []));

        content.AddVariant(new MechanicVariant(
            "death.instant_food", "death.handler", "instant_food", null,
            "unlock.core", [], 0, new Dictionary<string, string>()));
        content.AddVariant(new MechanicVariant(
            "death.corpse_pulse", "death.handler", "corpse_pulse", "evolution.corpse.lifecycle",
            "unlock.core", [], 10, new Dictionary<string, string>
            {
                ["mode"] = "pulse",
                ["interval"] = "1.25",
            }));
        content.AddVariant(new MechanicVariant(
            "spawn.base", "spawn.policy", "base_spawn", null,
            "unlock.core", [], 0, new Dictionary<string, string>()));
        content.AddVariant(new MechanicVariant(
            "spawn.enrichment", "spawn.policy", "food_enrichment_spawn", null,
            "unlock.spawn.enrichment", [], 20, new Dictionary<string, string>
            {
                ["chance"] = "0.08",
            }));
    }

    private static void AddContent(GameplayContentRegistry content, string id, string kind, string unlockId) =>
        content.AddContent(new GameplayContentEntry(id, kind, unlockId));
}

/// <summary>Immutable gameplay runtime: content snapshot, progression, resolver, pipeline.</summary>
public sealed class GameplayRuntime
{
    public GameplayRuntime(
        GameplayContentSnapshot content,
        GameplayProgression progression,
        GameplayModuleHost modules,
        GameplayPipeline pipeline)
    {
        Content = content;
        BaseContent = content;
        Progression = progression;
        Unlocks = new UnlockService(content, progression);
        Resolver = new MechanicResolver(content, Unlocks);
        ConfigureGenomeGate();
        Modules = modules;
        Pipeline = pipeline;
        Progression.UnlockedIds.Add("unlock.core");
    }

    public GameplayContentSnapshot Content { get; private set; }
    private GameplayContentSnapshot BaseContent { get; }
    public GameplayProgression Progression { get; }
    public UnlockService Unlocks { get; private set; }
    public MechanicResolver Resolver { get; private set; }
    public GameplayModuleHost Modules { get; }
    public GameplayPipeline Pipeline { get; }

    /// <summary>
    ///     Atomically merges an external content snapshot into the gameplay
    ///     runtime. Existing progression and systems are preserved; duplicate ids
    ///     are rejected by the registry.
    /// </summary>
    public void ApplyExternalContent(GameplayContentSnapshot external)
    {
        ArgumentNullException.ThrowIfNull(external);
        var registry = new GameplayContentRegistry();
        registry.AddSnapshot(Content);
        registry.AddSnapshot(external);
        Content = registry.BuildSnapshot();
        Unlocks = new UnlockService(Content, Progression);
        Resolver = new MechanicResolver(Content, Unlocks);
        ConfigureGenomeGate();
    }

    /// <summary>Applies an external JSON pack while retaining core content and progression.</summary>
    public void ApplyExternalPack(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ApplyExternalPacks([json]);
    }

    /// <summary>
    ///     Rebuilds the merged content from the built-in base plus all external
    ///     packs. Safe to call repeatedly when content files change.
    /// </summary>
    public void ApplyExternalPacks(IEnumerable<string> packJson)
    {
        ArgumentNullException.ThrowIfNull(packJson);
        var registry = new GameplayContentRegistry();
        registry.AddSnapshot(BaseContent);
        foreach (var json in packJson)
            GameplayContentPackLoader.AddTo(registry, json);
        Content = registry.BuildSnapshot();
        Unlocks = new UnlockService(Content, Progression);
        Resolver = new MechanicResolver(Content, Unlocks);
        ConfigureGenomeGate();
    }

    public static GameplayRuntime CreateDefault()
    {
        var modules = new GameplayModuleHost();
        modules.AddModule(new DefaultGameplayContentModule());
        var (content, services) = modules.Load();
        var progression = new GameplayProgression();
        var pipeline = new GameplayPipeline().FromServices(services);
        return new GameplayRuntime(content, progression, modules, pipeline);
    }

    /// <summary>Demo-scoped runtime; replaced by a scoped host during later migration.</summary>
    private void ConfigureGenomeGate()
    {
        GenomeContentGate.OrganFilter = organ =>
        {
            var id = GameplayContentIds.ForOrgan(organ);
            return id is null || Unlocks.IsContentAvailable(id);
        };
        GenomeContentGate.ActionFilter = action =>
        {
            var id = GameplayContentIds.ForAction(action);
            return id is null || Unlocks.IsContentAvailable(id);
        };
        GenomeContentGate.SensorFilter = sensor =>
        {
            var id = GameplayContentIds.ForSensor(sensor);
            return id is null || Unlocks.IsContentAvailable(id);
        };
    }

    public static GameplayRuntime? Current { get; set; }

    public static GameplayRuntime EnsureCurrent() => Current ??= CreateDefault();
}
