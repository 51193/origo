using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Save.Meta;
using Origo.Core.Snd;
using Origo.GodotAdapter.Bootstrap;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>
///     Root entry for the evolution demo. The SND level is the pond itself;
///     main menu / pause / save panels are Godot UI overlays, so loading a save
///     never has to swap levels or rebuild the presentation tree.
/// </summary>
public partial class DemoEntry : OrigoDefaultEntry
{
    public const string SaveRoot = "user://evolution_demo_saves";
    public const string AutoSaveId = "auto";
    public const string IterateCheckpointSlotPrefix = "iterate_";

    /// <summary>Current genome/save schema version produced by the demo.</summary>
    public const int GenomeVersion = 7;
    public const string ControllerName = "world_controller";

    /// <summary>Per-context gameplay services.</summary>
    public EvolutionGameSession Session { get; private set; } = null!;

    /// <summary>Account/world progression persistence boundary.</summary>
    public GameplayPersistence Persistence { get; private set; } = null!;

    /// <summary>UI and world view are created after the Origo context is ready.</summary>
    public DemoUi Ui { get; private set; } = null!;

    /// <summary>Pond renderer created after the Origo context is ready.</summary>
    public EvolutionWorldView WorldView { get; private set; } = null!;

    /// <inheritdoc/>
    public override void _Ready()
    {
        var gameplayMode = string.Equals(
            OS.GetEnvironment("ORIGO_DEMO_SMOKE"), "1", StringComparison.Ordinal)
            ? GameplayMode.Sandbox
            : GameplayMode.Campaign;
        var gameplayRuntime = GameplayRuntime.CreateDefault([new CoreEvolutionModule()], gameplayMode);
        DemoMechanicHandlers.Validate(gameplayRuntime);
        ConfigPath = "res://Origo/entry/entry.json";
        SceneAliasMapPath = "res://Origo/maps/scene_aliases.map";
        SndTemplateMapPath = "res://Origo/maps/snd_templates.map";
        InitialSaveRootPath = "res://Origo/initial";
        SaveRootPath = SaveRoot;
        SystemBlackboardSaveRoot = "user://evolution_demo_system";

        base._Ready();
        Session = new EvolutionGameSession(Context, () => Runtime.SessionManager);
        EvolutionGameSession.Register(Session);
        if (Runtime.SessionManager.ForegroundSession is null)
            Runtime.DriveFrame(0d);
        var profile = BoxUnlockProfile.All(gameplayRuntime.Content);
        var foreground = Runtime.SessionManager.ForegroundSession
            ?? throw new InvalidOperationException("The demo has no foreground session.");
        if (BoxProfileCodec.TryRead(foreground, out _, out _, out _, out _))
        {
            Session.AttachLoadedBox(foreground);
        }
        else
        {
            var seed = foreground.FindByName(ControllerName) is { } controller
                ? ReadInt(controller, DemoKeys.Seed)
                : 0;
            Session.RegisterForeground(profile, seed, foreground);
        }

        foreach (var key in Runtime.SessionManager.Keys)
        {
            var run = Runtime.SessionManager.TryGet(key);
            if (run is null || run.IsFrontSession)
                continue;
            Session.AttachLoadedBox(run);
        }

        Session.EnsureTopology();
        Persistence = new GameplayPersistence(Session);

        WorldView = new EvolutionWorldView(this);
        AddChild(WorldView);
        Ui = new DemoUi(this);
        AddChild(Ui);
        AddChild(new UnlockOverlay(this));
        AddChild(new DemoContentWatcher(Session));
        if (string.Equals(OS.GetEnvironment("ORIGO_DEMO_SMOKE"), "1", StringComparison.Ordinal))
            AddChild(new DemoSmokeRunner(this));
    }

    /// <inheritdoc/>
    protected override void ConfigureSaveMetadataContributors(ISndContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Save.RegisterSaveMetaContributor(static saveContext => BuildSaveMetadata(saveContext));
    }

    private static Dictionary<string, string> BuildSaveMetadata(SaveMetaBuildContext context)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var controller = context.SceneAccess.FindByName(ControllerName);
        if (controller is null)
            return metadata;

        var creatures = 0;
        var lineages = new HashSet<int>();
        foreach (var entity in context.SceneAccess.GetEntities())
        {
            var (isCreature, kind) = entity.TryGetData<string>(DemoKeys.Kind);
            if (!isCreature || !string.Equals(kind, DemoKeys.CreatureKind, StringComparison.Ordinal))
                continue;

            creatures++;
            if (entity.TryGetData<int>(DemoKeys.Lineage).found)
                lineages.Add(entity.TryGetData<int>(DemoKeys.Lineage).value);
        }

        var population = creatures.ToString(CultureInfo.InvariantCulture);
        metadata["demo.population"] = population;
        metadata["demo.time"] = ReadFloat(controller, DemoKeys.Time).ToString("0.0", CultureInfo.InvariantCulture);
        metadata["demo.generation"] = ReadInt(controller, DemoKeys.MaxGeneration).ToString(CultureInfo.InvariantCulture);
        metadata["demo.lineages"] = lineages.Count.ToString(CultureInfo.InvariantCulture);
        metadata["demo.mutation"] = $"{ReadFloat(controller, DemoKeys.MutationRate) * 100d:0.0}%";
        metadata["demo.seed"] = ReadInt(controller, DemoKeys.Seed).ToString(CultureInfo.InvariantCulture);
        metadata["demo.saved"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        metadata["demo.genomeVersion"] = ReadInt(controller, DemoKeys.GenomeVersion).ToString(CultureInfo.InvariantCulture);
        return metadata;
    }

    private static float ReadFloat(ISndEntity entity, string key)
    {
        var (found, value) = entity.TryGetData<float>(key);
        return found ? value : 0f;
    }

    private static int ReadInt(ISndEntity entity, string key)
    {
        var (found, value) = entity.TryGetData<int>(key);
        return found ? value : 0;
    }
}
