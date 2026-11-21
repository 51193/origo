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

        content.AddHandler(new GameplayHandlerDefinition("instant_food", [], []));
        content.AddHandler(new GameplayHandlerDefinition("corpse_pulse", ["interval"], ["interval"]));
        content.AddHandler(new GameplayHandlerDefinition("base_spawn", [], []));
        content.AddHandler(new GameplayHandlerDefinition("food_enrichment_spawn", [], []));

        const string core = "unlock.core";
        const string toughFood = "unlock.forage.tough_food";
        const string hardDigest = "unlock.forage.hard_digest";
        const string sprint = "unlock.forage.sprint";
        const string pond1 = "unlock.pond.1";
        const string toxicFood = "unlock.chem.toxic_food";
        const string toxinResistance = "unlock.chem.toxin_resistance";
        const string creatureSense = "unlock.sense.creature";
        const string rhythm = "unlock.sense.rhythm";
        const string pond2 = "unlock.pond.2";
        const string corpseCycle = "unlock.death.corpse";
        const string carrionSense = "unlock.carrion.sense";
        const string pond3 = "unlock.pond.3";
        const string predation = "unlock.predation.spike";
        const string battery = "unlock.predation.battery";
        const string armor = "unlock.predation.armor";
        const string pond4 = "unlock.pond.4";
        const string foodTypeVision = "unlock.vision.foodtype";
        const string enrichment = "unlock.spawn.enrichment";
        const string pond5 = "unlock.pond.5";
        const string pond6 = "unlock.pond.6";

        AddContent(content, "gene.organ.vision", "organ", core);
        AddContent(content, "gene.action.move", "action", core);
        AddContent(content, "gene.action.turn", "action", core);
        AddContent(content, "gene.sensor.food_ahead", "sensor", core);
        AddContent(content, "gene.sensor.food_left", "sensor", core);
        AddContent(content, "gene.sensor.food_right", "sensor", core);
        AddContent(content, "gene.sensor.wall_ahead", "sensor", core);
        AddContent(content, "gene.sensor.energy", "sensor", core);
        AddContent(content, "mechanic.food.tough", "mechanic", toughFood);
        AddContent(content, "gene.organ.hard_digest", "organ", hardDigest);
        AddContent(content, "gene.organ.sprint", "organ", sprint);
        AddContent(content, "gene.action.sprint", "action", sprint);
        AddContent(content, "mechanic.food.toxic", "mechanic", toxicFood);
        AddContent(content, "gene.organ.toxin_resistance", "organ", toxinResistance);
        AddContent(content, "gene.organ.creature_sense", "organ", creatureSense);
        AddContent(content, "gene.sensor.creature_ahead", "sensor", creatureSense);
        AddContent(content, "gene.organ.rhythm", "organ", rhythm);
        AddContent(content, "gene.sensor.sin", "sensor", rhythm);
        AddContent(content, "gene.sensor.cos", "sensor", rhythm);
        AddContent(content, "mechanic.death.instant_food", "mechanic", core);
        AddContent(content, "mechanic.spawn.base", "mechanic", core);
        AddContent(content, "mechanic.death.corpse_pulse", "mechanic", corpseCycle);
        AddContent(content, "gene.organ.carrion_sense", "organ", carrionSense);
        AddContent(content, "gene.sensor.corpse_ahead", "sensor", carrionSense);
        AddContent(content, "gene.organ.spike", "organ", predation);
        AddContent(content, "gene.action.spike", "action", predation);
        AddContent(content, "gene.action.aim", "action", predation);
        AddContent(content, "gene.organ.spike_battery", "organ", battery);
        AddContent(content, "gene.organ.armor", "organ", armor);
        AddContent(content, "gene.organ.food_type_vision", "organ", foodTypeVision);
        AddContent(content, "gene.sensor.food_soft", "sensor", foodTypeVision);
        AddContent(content, "gene.sensor.food_tough", "sensor", foodTypeVision);
        AddContent(content, "gene.sensor.food_toxic", "sensor", foodTypeVision);
        AddContent(content, "gene.sensor.food_value_ahead", "sensor", foodTypeVision);
        AddContent(content, "gene.sensor.food_value_left", "sensor", foodTypeVision);
        AddContent(content, "gene.sensor.food_value_right", "sensor", foodTypeVision);
        AddContent(content, "mechanic.spawn.enrichment", "mechanic", enrichment);

        void AddUnlock(string id, string title, string category, long cost,
            IReadOnlyList<string> prerequisites, IReadOnlyList<string> grants,
            IReadOnlyList<string> requiredMilestones) =>
            content.AddUnlock(new UnlockDefinition(
                id, title, category, cost, prerequisites, grants, requiredMilestones));

        AddUnlock(core, "Core evolution", "core", 0, [], [], []);
        AddUnlock(toughFood, "Tough flora", "foraging", 20, [core], ["mechanic.food.tough"], ["milestone.firstBirth"]);
        AddUnlock(hardDigest, "Hard digestion", "foraging", 30, [toughFood], ["gene.organ.hard_digest"], ["milestone.toughDiscovered"]);
        AddUnlock(sprint, "Sprint", "foraging", 25, [core], ["gene.organ.sprint", "gene.action.sprint"], ["milestone.generation5"]);
        AddUnlock(pond1, "Tiny bloom expansion", "pond", 30, [core], [], ["milestone.population32"]);
        AddUnlock(toxicFood, "Toxic flora", "chemistry", 25, [toughFood], ["mechanic.food.toxic"], ["milestone.hardGut"]);
        AddUnlock(toxinResistance, "Toxin resistance", "chemistry", 30, [toxicFood], ["gene.organ.toxin_resistance"], ["milestone.toxinDiscovered"]);
        AddUnlock(creatureSense, "Creature sense", "senses", 25, [core], ["gene.organ.creature_sense", "gene.sensor.creature_ahead"], ["milestone.population32"]);
        AddUnlock(rhythm, "Rhythm", "senses", 25, [core], ["gene.organ.rhythm", "gene.sensor.sin", "gene.sensor.cos"], ["milestone.population32"]);
        AddUnlock(pond2, "Shallow expansion", "pond", 60, [pond1], [], ["milestone.toxinGut"]);
        AddUnlock(corpseCycle, "Corpse cycle", "carrion", 35, [core], ["mechanic.death.corpse_pulse"], ["milestone.toxinGut"]);
        AddUnlock(carrionSense, "Carrion sense", "carrion", 25, [corpseCycle], ["gene.organ.carrion_sense", "gene.sensor.corpse_ahead"], ["milestone.firstCarrion"]);
        AddUnlock(pond3, "Pond expansion III", "pond", 80, [pond2], [], ["milestone.firstCarrion"]);
        AddUnlock(predation, "Predation", "predation", 70, [core], ["gene.organ.spike", "gene.action.spike", "gene.action.aim"], ["milestone.population800"]);
        AddUnlock(battery, "Spike battery", "predation", 35, [predation], ["gene.organ.spike_battery"], ["milestone.firstBlood"]);
        AddUnlock(armor, "Armor", "predation", 40, [predation], ["gene.organ.armor"], ["milestone.firstBlood"]);
        AddUnlock(pond4, "Pond expansion IV", "pond", 100, [pond3], [], ["milestone.population100"]);
        AddUnlock(foodTypeVision, "Food-type vision", "vision", 120, [toxinResistance], ["gene.organ.food_type_vision", "gene.sensor.food_soft", "gene.sensor.food_tough", "gene.sensor.food_toxic", "gene.sensor.food_value_ahead", "gene.sensor.food_value_left", "gene.sensor.food_value_right"], ["milestone.population300"]);
        AddUnlock(enrichment, "Enrichment spawning", "ecology", 50, [core], ["mechanic.spawn.enrichment"], ["milestone.population100"]);
        AddUnlock(pond5, "Pond expansion V", "pond", 140, [pond4], [], ["milestone.longDrift"]);
        AddUnlock(pond6, "Pond expansion VI", "pond", 180, [pond5], [], ["milestone.deepTime"]);

        void AddMilestone(string id, string eventKey, double threshold, long reward) =>
            content.AddMilestone(new MilestoneDefinition(id, eventKey, threshold, reward, [], []));
        AddMilestone("milestone.firstBreath", "lifetimeSeconds", 10d, 10);
        AddMilestone("milestone.firstMeal", "mealsTotal", 1d, 10);
        AddMilestone("milestone.firstBirth", "births", 1d, 20);
        AddMilestone("milestone.generation5", "maxGeneration", 5d, 20);
        AddMilestone("milestone.population32", "population", 32d, 30);
        AddMilestone("milestone.toughDiscovered", "toughMeals", 10d, 30);
        AddMilestone("milestone.hardGut", "organHardDigest", 1d, 40);
        AddMilestone("milestone.toxinDiscovered", "toxicMeals", 10d, 40);
        AddMilestone("milestone.toxinGut", "organToxinResistance", 1d, 50);
        AddMilestone("milestone.firstBlood", "kills", 1d, 50);
        AddMilestone("milestone.firstCarrion", "corpseMeals", 1d, 50);
        AddMilestone("milestone.population100", "population", 100d, 100);
        AddMilestone("milestone.population300", "population", 300d, 150);
        AddMilestone("milestone.longDrift", "lifetimeSeconds", 600d, 80);
        AddMilestone("milestone.population800", "population", 800d, 150);
        AddMilestone("milestone.deepTime", "lifetimeSeconds", 1800d, 150);
        AddMilestone("milestone.population2000", "population", 2000d, 250);
        AddMilestone("milestone.enduringWeb", "lifetimeSeconds", 3600d, 300);

        content.AddVariant(new MechanicVariant(
            "death.instant_food", "death.handler", "instant_food", null,
            core, [], 0, new Dictionary<string, string>()));
        content.AddVariant(new MechanicVariant(
            "death.corpse_pulse", "death.handler", "corpse_pulse", "evolution.corpse.lifecycle",
            corpseCycle, [], 10, new Dictionary<string, string>
            {
                ["mode"] = "pulse",
                ["interval"] = "1.25",
            }));
        content.AddVariant(new MechanicVariant(
            "spawn.base", "spawn.policy", "base_spawn", null,
            core, [], 0, new Dictionary<string, string>()));
        content.AddVariant(new MechanicVariant(
            "spawn.enrichment", "spawn.policy", "food_enrichment_spawn", null,
            enrichment, [], 20, new Dictionary<string, string>
            {
                ["chance"] = "0.08",
            }));
    }

    private static void AddContent(GameplayContentRegistry content, string id, string kind, string unlockId) =>
        content.AddContent(new GameplayContentEntry(id, kind, unlockId));
}

/// <summary>World metrics consumed by the declarative milestone evaluator.</summary>
public readonly record struct MilestoneMetrics(
    double LifetimeSeconds = 0d,
    double Population = 0d,
    double Kills = 0d,
    double MealsTotal = 0d,
    double ToughMeals = 0d,
    double ToxicMeals = 0d,
    double CorpseMeals = 0d,
    double Births = 0d,
    double MaxGeneration = 0d,
    double Lineages = 0d,
    double HardDigestCreatures = 0d,
    double ToxinResistanceCreatures = 0d);

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

    /// <summary>Sets campaign/sandbox behavior and refreshes the content gates.</summary>
    public void SetMode(GameplayMode mode)
    {
        Progression.Mode = mode;
        RefreshContentGates();
    }

    /// <summary>Re-applies content gates after a progression or profile change.</summary>
    public void RefreshContentGates() => ConfigureGenomeGate();

    /// <summary>Consumes pending unlock innovations; the simulation spawns carriers.</summary>
    public IReadOnlyList<string> TakePendingInnovations()
    {
        if (Progression.PendingInnovationUnlocks.Count == 0)
            return [];
        var pending = Progression.PendingInnovationUnlocks.ToArray();
        Progression.PendingInnovationUnlocks.Clear();
        return pending;
    }

    /// <summary>Attempts a purchase and refreshes gates/injection state on success.</summary>
    public bool TryUnlock(string unlockId)
    {
        if (!Unlocks.TryUnlock(unlockId))
            return false;
        Progression.PendingInnovationUnlocks.Add(unlockId);
        RefreshContentGates();
        return true;
    }

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

    public static GameplayRuntime CreateDefault(
        IReadOnlyList<IGameplayModule>? modules = null,
        GameplayMode mode = GameplayMode.Sandbox)
    {
        var host = new GameplayModuleHost();
        if (modules is null || modules.Count == 0)
        {
            host.AddModule(new DefaultGameplayContentModule());
        }
        else
        {
            foreach (var module in modules)
                host.AddModule(module);
        }

        var (content, services) = host.Load();
        var progression = new GameplayProgression { Mode = mode };
        var pipeline = new GameplayPipeline().FromServices(services);
        return new GameplayRuntime(content, progression, host, pipeline);
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

    /// <summary>Advances passive points and evaluates milestones for one simulation tick.</summary>
    public void TickProgression(double deltaSeconds, MilestoneMetrics metrics)
    {
        if (Progression.Mode == GameplayMode.Sandbox)
            return;
        if (deltaSeconds > 0d)
        {
            Progression.LifetimeSeconds += deltaSeconds;
            if (Progression.Mode == GameplayMode.Campaign)
            {
                Progression.PassivePointProgress += deltaSeconds;
                while (Progression.PassivePointProgress >= GameplayProgression.PassivePointIntervalSeconds)
                {
                    Progression.PassivePointProgress -= GameplayProgression.PassivePointIntervalSeconds;
                    Progression.Points += GameplayProgression.PassivePointsPerCycle;
                }
            }
        }

        EvaluateMilestones(metrics with { LifetimeSeconds = Progression.LifetimeSeconds });
    }

    /// <summary>Evaluates declarative milestones against world metrics.</summary>
    public void EvaluateMilestones(double lifetimeSeconds, double population, double kills) =>
        EvaluateMilestones(new MilestoneMetrics(lifetimeSeconds, population, kills));

    /// <summary>Evaluates declarative milestones against world metrics.</summary>
    public void EvaluateMilestones(MilestoneMetrics metrics)
    {
        if (Content.Milestones.Count == 0)
            return;
        foreach (var milestone in Content.Milestones.Values)
        {
            if (Progression.CompletedMilestones.Contains(milestone.Id))
                continue;
            var value = milestone.EventKey switch
            {
                "lifetimeSeconds" => metrics.LifetimeSeconds,
                "population" => metrics.Population,
                "kills" or "spikeKills" => metrics.Kills,
                "mealsTotal" => metrics.MealsTotal,
                "toughMeals" => metrics.ToughMeals,
                "toxicMeals" => metrics.ToxicMeals,
                "corpseMeals" => metrics.CorpseMeals,
                "births" => metrics.Births,
                "maxGeneration" => metrics.MaxGeneration,
                "lineages" => metrics.Lineages,
                "organHardDigest" => metrics.HardDigestCreatures,
                "organToxinResistance" => metrics.ToxinResistanceCreatures,
                _ => double.MinValue,
            };
            if (value < milestone.Threshold)
                continue;
            Progression.CompletedMilestones.Add(milestone.Id);
            Progression.Points += milestone.RewardPoints;
            foreach (var unlock in milestone.UnlockGrants ?? [])
                Progression.UnlockedIds.Add(unlock);
        }
    }

}
