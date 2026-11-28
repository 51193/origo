using System;
using System.IO;
using System.Linq;
using Godot;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>
///     Optional automated acceptance runner for local/CI smoke checks:
///     ORIGO_DEMO_SMOKE=1 starts a deterministic pond, lets it run, saves,
///     reloads, and verifies that the population and simulation time survive.
///     It is not part of the normal player flow.
/// </summary>
public partial class DemoSmokeRunner : Node
{
    private const int StartupFrameLimit = 600;
    private const int RunningFrameLimit = 6000;
    private const float MinimumSmokeGenerationSeconds = 30f;
    private const string SmokeSlot = "smoke_slot";

    private readonly DemoEntry _entry;
    private readonly float _targetSeconds;
    private readonly int _smokeSeed;
    private readonly float _fastForwardTarget;
    private readonly string _benchmarkSaveSlot;
    private readonly float _checkpointInterval;
    private int _state;
    private int _frame;
    private int _savedPopulation;
    private string? _migrationMigrantName;
    private string? _migrationGenome;
    private string? _migrationSourceBox;
    private ArenaMatch? _arenaMatch;
    private ArenaResult? _arenaResult;
    private int _arenaPolls;
    private float _savedTime;
    private int _restoredCreatures;
    private int _restoredGeneration;
    private float _restoredTime;
    private int _restoredFood;
    private float _fastForwardStartTime;
    private int _resetPopulation;
    private bool _spedUp;
    private readonly string? _screenshotDir = OS.GetEnvironment("ORIGO_DEMO_SCREENSHOT_DIR");
    private bool _menuShotTaken;
    private bool _gameShotTaken;
    private bool _startInputClicked;
    private bool _diversityShotTaken;
    private bool _cameraTested;
    private readonly CampaignPurchaseStrategy? _campaignStrategy;
    private readonly GameplayMode _smokeMode;
    private readonly bool _campaignFresh;
    private readonly bool _allowEnrichment;
    private readonly bool _allowPredation;
    private readonly float _purchaseInterval;
    private readonly string? _purchaseLogPath;
    private int _campaignPurchases;
    private float _firstPurchaseTime = -1f;
    private float _lastPurchaseTime = -1f;
    private float _nextPurchaseTime = -1f;
    private bool _purchaseLogStarted;
    private string? _campaignConfigurationError;
    private int _verifiedCheckpoints;
    private string? _configurationError;
    private readonly DateTime _runStartedUtc = DateTime.UtcNow;

    public DemoSmokeRunner(DemoEntry entry)
    {
        _entry = entry;
        _targetSeconds = float.TryParse(
            OS.GetEnvironment("ORIGO_DEMO_SMOKE_SECONDS"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var seconds)
            ? Math.Max(2f, seconds)
            : 2f;
        _smokeSeed = int.TryParse(
            OS.GetEnvironment("ORIGO_DEMO_SMOKE_SEED"),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var seed)
            ? seed
            : 20261101;
        _fastForwardTarget = float.TryParse(
            OS.GetEnvironment("ORIGO_DEMO_SMOKE_FAST_FORWARD"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var fastForward)
            ? Math.Max(1f, fastForward)
            : 3f;
        _benchmarkSaveSlot = OS.GetEnvironment("ORIGO_DEMO_SMOKE_SAVE_SLOT");
        _checkpointInterval = float.TryParse(
            OS.GetEnvironment("ORIGO_DEMO_SMOKE_CHECKPOINT_INTERVAL"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var checkpointInterval)
            ? checkpointInterval
            : 0f;
        if (_checkpointInterval < 0f
            || (_checkpointInterval > 0f
                && _checkpointInterval < IterateCheckpointSchedule.MinimumIntervalSeconds))
        {
            _configurationError =
                "ORIGO_DEMO_SMOKE_CHECKPOINT_INTERVAL must be 0 or at least " +
                $"{IterateCheckpointSchedule.MinimumIntervalSeconds:0} simulation seconds.";
        }

        var strategyText = OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_STRATEGY").Trim();
        var autoplayText = OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_AUTOPLAY").Trim();
        if (strategyText.Length > 0)
        {
            if (!TryParseStrategy(strategyText, out var parsed))
            {
                _campaignConfigurationError =
                    $"Unknown ORIGO_DEMO_CAMPAIGN_STRATEGY '{strategyText}'; expected greedy, planned, cheapest, or idle.";
            }
            else
            {
                _campaignStrategy = parsed;
            }
        }
        else if (autoplayText is "greedy" or "planned" or "cheapest" or "idle")
        {
            _campaignStrategy = ParseStrategy(autoplayText);
        }
        else if (string.Equals(autoplayText, "1", StringComparison.Ordinal))
        {
            _campaignStrategy = CampaignPurchaseStrategy.Planned;
        }

        _smokeMode = _campaignStrategy is null ? GameplayMode.Sandbox : GameplayMode.Campaign;
        _campaignFresh = ParseBoolean(
            OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_FRESH"), defaultValue: true,
            "ORIGO_DEMO_CAMPAIGN_FRESH", ref _configurationError);
        _allowEnrichment = ParseBoolean(
            OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_ENRICHMENT"), defaultValue: true,
            "ORIGO_DEMO_CAMPAIGN_ENRICHMENT", ref _configurationError);
        _allowPredation = ParseBoolean(
            OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_PREDATION"), defaultValue: true,
            "ORIGO_DEMO_CAMPAIGN_PREDATION", ref _configurationError);
        var defaultInterval = _campaignStrategy switch
        {
            CampaignPurchaseStrategy.Greedy => 0f,
            CampaignPurchaseStrategy.Planned => 10f,
            CampaignPurchaseStrategy.Cheapest => 15f,
            CampaignPurchaseStrategy.Idle => 300f,
            _ => 0f,
        };
        _purchaseInterval = float.TryParse(
            OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_PURCHASE_INTERVAL"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var purchaseInterval)
            ? purchaseInterval
            : defaultInterval;
        if (_campaignStrategy is not null
            && (!float.IsFinite(_purchaseInterval)
                || _purchaseInterval < 0f
                || (_campaignStrategy != CampaignPurchaseStrategy.Greedy && _purchaseInterval <= 0f)))
        {
            _campaignConfigurationError =
                "ORIGO_DEMO_CAMPAIGN_PURCHASE_INTERVAL must be positive for planned/cheapest/idle and non-negative for greedy.";
        }

        _purchaseLogPath = OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_PURCHASE_LOG").Trim() is { Length: > 0 } path
            ? path
            : null;
    }

    private static bool TryParseStrategy(string text, out CampaignPurchaseStrategy strategy)
    {
        switch (text)
        {
            case "greedy":
                strategy = CampaignPurchaseStrategy.Greedy;
                return true;
            case "planned":
                strategy = CampaignPurchaseStrategy.Planned;
                return true;
            case "cheapest":
                strategy = CampaignPurchaseStrategy.Cheapest;
                return true;
            case "idle":
                strategy = CampaignPurchaseStrategy.Idle;
                return true;
            default:
                strategy = default;
                return false;
        }
    }

    private static CampaignPurchaseStrategy ParseStrategy(string text) =>
        text switch
        {
            "greedy" => CampaignPurchaseStrategy.Greedy,
            "planned" => CampaignPurchaseStrategy.Planned,
            "cheapest" => CampaignPurchaseStrategy.Cheapest,
            "idle" => CampaignPurchaseStrategy.Idle,
            _ => throw new InvalidOperationException($"Unknown campaign strategy '{text}'."),
        };

    private static bool ParseBoolean(
        string text, bool defaultValue, string option, ref string? error)
    {
        if (text.Length == 0)
            return defaultValue;
        if (string.Equals(text, "1", StringComparison.Ordinal))
            return true;
        if (string.Equals(text, "0", StringComparison.Ordinal))
            return false;
        error ??= $"{option} must be 0 or 1.";
        return defaultValue;
    }

    public override void _Process(double delta)
    {
        try
        {
            if (_configurationError is not null)
                throw new InvalidOperationException(_configurationError);
            if (_campaignConfigurationError is not null)
                throw new InvalidOperationException(_campaignConfigurationError);
            Tick();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"DEMO_SMOKE_FAILED {ex.GetType().Name}: {ex.Message}");
            GetTree().Quit(4);
        }
    }

    private bool HasFoodOutsidePond()
    {
        var session = _entry.Runtime.SessionManager.ForegroundSession;
        if (session is null)
            return false;
        foreach (var entity in session.GetEntities())
        {
            var (isFood, kind) = entity.TryGetData<string>(DemoKeys.Kind);
            if (!isFood || !string.Equals(kind, DemoKeys.FoodKind, StringComparison.Ordinal))
                continue;

            var (hasX, x) = entity.TryGetData<float>(DemoKeys.X);
            var (hasY, y) = entity.TryGetData<float>(DemoKeys.Y);
            if (!hasX || !hasY)
                continue;
            if ((x * x) + (y * y) > Tuning.PondRadius * Tuning.PondRadius)
                return true;
        }

        return false;
    }

    private static void AssertWithinCaps((int creatures, int food, int generation, float time, string phase) stats)
    {
        if (stats.creatures > Tuning.PopulationCap)
            throw new InvalidOperationException(
                $"Population {stats.creatures} exceeds the cap {Tuning.PopulationCap}.");
        if (stats.food > Tuning.FoodCap)
            throw new InvalidOperationException(
                $"Food {stats.food} exceeds the cap {Tuning.FoodCap}.");
    }

    private void AssertCameraFit()
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 180f || viewport.Y <= 160f)
            return;
        var expected = MathF.Max(
            0.02f,
            MathF.Min(
                (viewport.X - 180f) / (Tuning.PondRadius * 2f),
                (viewport.Y - 160f) / (Tuning.PondRadius * 2f)));
        if (MathF.Abs(_entry.WorldView.FitViewScale - expected) > 0.001f)
            throw new InvalidOperationException(
                $"Camera fit {_entry.WorldView.FitViewScale:0.0000} is stale for pond radius {Tuning.PondRadius:0}; expected {expected:0.0000}.");
    }

    private Vector2 ToWindowPosition(Vector2 viewportPosition)
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var windowSize = DisplayServer.WindowGetSize();
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
            return viewportPosition;
        return new Vector2(
            viewportPosition.X * windowSize.X / viewportSize.X,
            viewportPosition.Y * windowSize.Y / viewportSize.Y);
    }

    private void Capture(string fileName)
    {
        if (string.IsNullOrWhiteSpace(_screenshotDir))
            return;
        try
        {
            System.IO.Directory.CreateDirectory(_screenshotDir);
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(System.IO.Path.Combine(_screenshotDir, fileName));
            GD.Print($"DEMO_SCREENSHOT {fileName}");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"DEMO_SCREENSHOT failed: {ex.Message}");
        }
    }

    private void Tick()
    {
        var ui = _entry.Ui;
        if (ui is null)
            return;

        if (_campaignStrategy is not null
            && _state == 6
            && _entry.Ui.IsFastForwardingForSmoke
            && _entry.Session.Runtime.Progression.Mode == GameplayMode.Campaign)
            TryCampaignPurchases();

        switch (_state)
        {
            case 0:
                if (!ui.IsReadyForSmoke)
                    return;
                if (!_menuShotTaken)
                {
                    _frame++;
                    if (_frame < 3)
                        return;
                    _menuShotTaken = true;
                    Capture("menu.png");
                    if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.Ordinal))
                    {
                        ui.StartSmokeGame(_smokeSeed, _smokeMode);
                        _state = 1;
                        _frame = 0;
                        break;
                    }

                    if (!ui.TryGetMenuButtonCenter("New Game", out var buttonCenter))
                        throw new InvalidOperationException("The main menu New Game button was not found.");
                    buttonCenter = ToWindowPosition(buttonCenter);
                    Input.ParseInputEvent(new InputEventMouseMotion
                    {
                        Position = buttonCenter,
                        GlobalPosition = buttonCenter,
                    });
                    Input.ParseInputEvent(new InputEventMouseButton
                    {
                        Position = buttonCenter,
                        GlobalPosition = buttonCenter,
                        ButtonIndex = MouseButton.Left,
                        Pressed = true,
                    });
                    Input.ParseInputEvent(new InputEventMouseButton
                    {
                        Position = buttonCenter,
                        GlobalPosition = buttonCenter,
                        ButtonIndex = MouseButton.Left,
                        Pressed = false,
                    });
                    _frame = 0;
                    _state = 10;
                    break;
                }
                ui.StartSmokeGame(_smokeSeed, _smokeMode);
                _state = 1;
                _frame = 0;
                break;

            case 10:
                _frame++;
                if (_frame > 30)
                    throw new InvalidOperationException("Clicking the main menu New Game button did not open the panel.");
                if (!ui.IsNewGamePanelVisibleForSmoke)
                    break;
                GD.Print("DEMO_MENU_INPUT_OK");
                if (!ui.TryGetVisibleButtonCenter("Start", out var startCenter))
                    throw new InvalidOperationException("The New Game Start button was not found.");
                startCenter = ToWindowPosition(startCenter);
                Input.ParseInputEvent(new InputEventMouseMotion
                {
                    Position = startCenter,
                    GlobalPosition = startCenter,
                });
                Input.ParseInputEvent(new InputEventMouseButton
                {
                    Position = startCenter,
                    GlobalPosition = startCenter,
                    ButtonIndex = MouseButton.Left,
                    Pressed = true,
                });
                Input.ParseInputEvent(new InputEventMouseButton
                {
                    Position = startCenter,
                    GlobalPosition = startCenter,
                    ButtonIndex = MouseButton.Left,
                    Pressed = false,
                });
                _startInputClicked = true;
                _state = 1;
                _frame = 0;
                break;

            case 1:
                _frame++;
                if (_frame > StartupFrameLimit)
                    throw new InvalidOperationException("The pond did not start.");
                var started = ui.SmokeStats();
                if (started.phase is DemoKeys.PhaseRunning or DemoKeys.PhaseStarting && started.creatures > 0)
                {
                    if (_startInputClicked)
                    {
                        _startInputClicked = false;
                        GD.Print("DEMO_START_INPUT_OK");
                    }

                    if (HasFoodOutsidePond())
                        throw new InvalidOperationException("A food entity spawned outside the pond boundary.");
                    if (!_cameraTested)
                    {
                        var view = _entry.WorldView;
                        var startScale = view.ViewScale;
                        view.ZoomAt(EvolutionWorldView.PondCenter, 1.25f);
                        if (view.ViewScale <= startScale)
                            throw new InvalidOperationException("Camera zoom-in did not change the view scale.");
                        var startFocus = view.Focus;
                        view.Pan(new Vector2(24f, 0f));
                        if (view.Focus == startFocus)
                            throw new InvalidOperationException("Camera pan did not change the focus.");
                        view.ResetView();
                        if (Mathf.Abs(view.ViewScale - view.FitViewScale) > 0.001f)
                            throw new InvalidOperationException("Camera reset did not restore the fit scale.");
                        view.RestoreDefaultView();
                        _cameraTested = true;
                        GD.Print("DEMO_CAMERA_OK");
                    }

                    if (!_spedUp)
                    {
                        ui.SetSmokeSpeed(8);
                        _spedUp = true;
                    }

                    _state = 2;
                    _frame = 0;
                }
                else if (started.phase == DemoKeys.PhaseExtinct)
                    throw new InvalidOperationException("The pond went extinct before the smoke run.");
                break;

            case 2:
                _frame++;
                if (_frame > RunningFrameLimit)
                    throw new InvalidOperationException("The pond did not accumulate simulation time.");
                var running = ui.SmokeStats();
                AssertWithinCaps(running);
                var generationDeadline = MathF.Max(_targetSeconds, MinimumSmokeGenerationSeconds);
                if (_spedUp && CampaignSmokeGate.ShouldWaitForGeneration(
                    running.time, _targetSeconds, generationDeadline,
                    running.generation, running.creatures))
                {
                    if (_frame % 30 == 0 && HasFoodOutsidePond())
                        throw new InvalidOperationException("A food entity left the pond boundary during the smoke run.");
                    break;
                }
                if (!_gameShotTaken)
                {
                    _gameShotTaken = true;
                    Capture("game.png");
                }
                if (!_diversityShotTaken
                    && !string.IsNullOrEmpty(_screenshotDir)
                    && !string.Equals(DisplayServer.GetName(), "headless", StringComparison.Ordinal)
                    && ui.TryGetVisibleButtonCenter("Diversity", out var diversityCenter))
                {
                    _diversityShotTaken = true;
                    diversityCenter = ToWindowPosition(diversityCenter);
                    Input.ParseInputEvent(new InputEventMouseMotion
                    {
                        Position = diversityCenter,
                        GlobalPosition = diversityCenter,
                    });
                    Input.ParseInputEvent(new InputEventMouseButton
                    {
                        Position = diversityCenter,
                        GlobalPosition = diversityCenter,
                        ButtonIndex = MouseButton.Left,
                        Pressed = true,
                    });
                    Input.ParseInputEvent(new InputEventMouseButton
                    {
                        Position = diversityCenter,
                        GlobalPosition = diversityCenter,
                        ButtonIndex = MouseButton.Left,
                        Pressed = false,
                    });
                    _state = 20;
                    _frame = 0;
                    break;
                }
                if (running.creatures == 0)
                    throw new InvalidOperationException("The pond went extinct during the smoke run.");
                _savedPopulation = running.creatures;
                _savedTime = running.time;
                ui.SaveSmokeGame(SmokeSlot);
                _state = 3;
                _frame = 0;
                break;

            case 20:
                _frame++;
                if (_frame < 8)
                    break;
                Capture("diversity.png");
                ui.CloseSmokePanel();
                _state = 2;
                _frame = 0;
                break;

            case 3:
                _frame++;
                if (_frame > StartupFrameLimit)
                    throw new InvalidOperationException("Smoke save did not complete.");
                if (ui.IsBusyForSmoke)
                    break;
                if (!_entry.Context.Save.ListSaves().Contains(SmokeSlot, StringComparer.Ordinal))
                    throw new InvalidOperationException("Smoke save is missing.");
                ui.LoadSmokeGame(SmokeSlot);
                _state = 4;
                _frame = 0;
                break;

            case 4:
                _frame++;
                if (_frame > StartupFrameLimit)
                    throw new InvalidOperationException("Smoke load did not complete.");
                if (ui.IsBusyForSmoke)
                    break;
                var restored = ui.SmokeStats();
                AssertWithinCaps(restored);
                if (restored.creatures == 0)
                    throw new InvalidOperationException("Loaded pond has no creatures.");
                if (restored.generation < 1)
                    throw new InvalidOperationException("The smoke run did not reach generation 1 (the second generation).");
                if (restored.time + 0.01f < _savedTime)
                    throw new InvalidOperationException(
                        $"Loaded time regressed: saved={_savedTime:0.0}, restored={restored.time:0.0}.");
                _restoredCreatures = restored.creatures;
                _restoredGeneration = restored.generation;
                _restoredTime = restored.time;
                _restoredFood = restored.food;
                if (_campaignStrategy is not null && _campaignFresh)
                    _entry.Session.Runtime.ResetCampaignProgression();
                ui.SetSmokeCheckpointInterval(_checkpointInterval);
                ui.StartSmokeGame(_smokeSeed, _smokeMode);
                _state = 5;
                _frame = 0;
                break;

            case 5:
                _frame++;
                if (_frame > StartupFrameLimit)
                    throw new InvalidOperationException("A fresh pond did not start after loading a save.");
                if (ui.IsBusyForSmoke)
                    break;
                var reset = ui.SmokeStats();
                if (reset.phase is not (DemoKeys.PhaseRunning or DemoKeys.PhaseStarting) || reset.creatures == 0)
                    break;
                if (reset.time > 5f)
                    throw new InvalidOperationException($"Fresh pond time did not reset (t={reset.time:0.0}).");
                _fastForwardStartTime = reset.time;
                _resetPopulation = reset.creatures;
                ui.StartFastForwardForSmoke(_fastForwardTarget);
                _state = 6;
                _frame = 0;
                break;

            case 6:
                _frame++;
                var checkpointCount = IterateCheckpointSchedule.Marks(
                    0f, _fastForwardTarget, _checkpointInterval).Count;
                var fastForwardFrameLimit = Math.Max(
                    StartupFrameLimit * 4,
                    (int)(_fastForwardTarget / Tuning.SimulationTickSeconds)
                    + (StartupFrameLimit * 4) + (checkpointCount * 120));
                if (_frame > fastForwardFrameLimit)
                    throw new InvalidOperationException("Timed iteration did not complete.");
                if (ui.IsFastForwardingForSmoke)
                    break;
                var fastForwarded = ui.SmokeStats();
                AssertWithinCaps(fastForwarded);
                if (fastForwarded.time < _fastForwardStartTime + 2f)
                    throw new InvalidOperationException("Timed iteration did not advance simulation time.");
                var expectedCheckpoints = VerifyIterateCheckpoints();
                if (!TryFinishArenaSmoke())
                    break;
                if (!string.IsNullOrWhiteSpace(_benchmarkSaveSlot))
                {
                    RunMigrationSmoke();
                    ui.SaveSmokeGame(_benchmarkSaveSlot);
                    _savedPopulation = fastForwarded.creatures;
                    _savedTime = fastForwarded.time;
                    _state = 7;
                    _frame = 0;
                    break;
                }

                RunMigrationSmoke();
                ValidateMigratedAfterLoad();
                ValidateBoxTopology();
                PrintCampaignSummary();
                GD.Print($"DEMO_SMOKE_OK population={_restoredCreatures} saved_population={_savedPopulation} " +
                    $"generation={_restoredGeneration} time={_restoredTime:0.0} food={_restoredFood} " +
                    $"reset_population={_resetPopulation} fast_forward_time={fastForwarded.time:0.0} " +
                    $"purchases={_campaignPurchases} " +
                    $"checkpoints={expectedCheckpoints}");
                GetTree().Quit(0);
                break;

            case 7:
                _frame++;
                if (_frame > StartupFrameLimit * 4)
                    throw new InvalidOperationException("Benchmark save did not complete.");
                if (ui.IsBusyForSmoke)
                    break;
                if (!_entry.Context.Save.ListSaves().Contains(_benchmarkSaveSlot, StringComparer.Ordinal))
                    throw new InvalidOperationException("Benchmark save is missing.");
                AssertSaveWrittenByThisRun(_benchmarkSaveSlot);
                if (!TryFinishArenaSmoke())
                    break;
                ValidateMigratedAfterLoad();
                ValidateBoxTopology();
                PrintCampaignSummary();
                GD.Print($"DEMO_BENCHMARK_OK seed={_smokeSeed} population={_savedPopulation} time={_savedTime:0.0} " +
                    $"slot={_benchmarkSaveSlot} checkpoints={_verifiedCheckpoints} " +
                    $"purchases={_campaignPurchases}");
                GetTree().Quit(0);
                break;
        }
    }

    private void PrintCampaignSummary()
    {
        if (_campaignStrategy is not { } strategy)
            return;
        var runtime = _entry.Session.Runtime;
        GD.Print($"DEMO_CAMPAIGN seed={_smokeSeed} strategy={strategy} " +
            $"enrichment={_allowEnrichment} predation={_allowPredation} " +
            $"points={runtime.Progression.Points} " +
            $"milestones={runtime.Progression.CompletedMilestones.Count} " +
            $"unlocks={runtime.Progression.UnlockedIds.Count} " +
            $"purchases={_campaignPurchases} " +
            $"firstPurchase={_firstPurchaseTime:0.###} lastPurchase={_lastPurchaseTime:0.###} " +
            $"mode={runtime.Progression.Mode}");
        AssertCameraFit();
    }

    private void TryCampaignPurchases()
    {
        if (_campaignStrategy is not { } strategy)
            return;
        var controller = _entry.Ui.GameplayController;
        if (controller is null)
            return;
        var time = controller.TryGetData<float>(DemoKeys.Time) is { found: true } timeData
            ? timeData.value
            : 0f;
        if (_nextPurchaseTime < 0f)
            _nextPurchaseTime = time + _purchaseInterval;
        if (time + 0.0001f < _nextPurchaseTime)
            return;
        var runtime = _entry.Session.Runtime;
        var options = new CampaignPurchaseOptions(_allowEnrichment, _allowPredation);
        var purchased = false;
        while (CampaignPurchasePolicy.Decide(runtime, strategy, options) is { } decision)
        {
            var pointsBefore = runtime.Progression.Points;
            if (!runtime.TryUnlock(decision.UnlockId))
                break;
            RecordPurchase(time, strategy, decision, pointsBefore, runtime.Progression.Points);
            purchased = true;
            if (strategy is CampaignPurchaseStrategy.Planned or CampaignPurchaseStrategy.Cheapest)
                break;
        }

        if (purchased)
            _entry.Ui.RefreshCampaignWorld();
        _nextPurchaseTime = strategy == CampaignPurchaseStrategy.Greedy
            ? time
            : time + _purchaseInterval;
    }

    private void RecordPurchase(
        float time,
        CampaignPurchaseStrategy strategy,
        CampaignPurchaseDecision decision,
        long pointsBefore,
        long pointsAfter)
    {
        _campaignPurchases++;
        if (_firstPurchaseTime < 0f)
            _firstPurchaseTime = time;
        _lastPurchaseTime = time;
        var line = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{time:0.###},{strategy},{decision.UnlockId},{decision.Cost},{pointsBefore},{pointsAfter}");
        GD.Print($"DEMO_CAMPAIGN_PURCHASE {line}");
        if (_purchaseLogPath is not { } path)
            return;
        if (!_purchaseLogStarted)
        {
            File.AppendAllText(path,
                "time,strategy,unlock,cost,points_before,points_after" + System.Environment.NewLine);
            _purchaseLogStarted = true;
        }

        File.AppendAllText(path, line + System.Environment.NewLine);
    }

    private bool TryFinishArenaSmoke()
    {
        if (_arenaMatch is null)
        {
            _arenaMatch = ArenaMatch.Start(
                _entry.Session, ["pond", "meadow"], perCohort: 6, _smokeSeed, durationSeconds: 0.5d);
            _arenaPolls = 0;
        }

        _arenaPolls++;
        if (_arenaPolls > StartupFrameLimit * 2)
            throw new InvalidOperationException("Arena smoke did not finish.");
        _arenaResult = _arenaMatch.Poll(_entry.Session);
        if (_arenaResult is null)
            return false;
        GD.Print("DEMO_ARENA_OK " + string.Join(' ',
            _arenaResult.Cohorts.Select(static cohort =>
                $"{cohort.SourceBoxId}:{cohort.Started}->{cohort.Survived}")) +
            $" winner={_arenaResult.Winner ?? "tie"} elapsed={_arenaResult.ElapsedSeconds:0.0}");
        _arenaMatch = null;
        return true;
    }

    private void RunMigrationSmoke()
    {
        var source = _entry.Session.TryGetBox("pond")
            ?? throw new InvalidOperationException("Migration smoke has no source box.");
        var target = _entry.Session.TryGetBox("meadow")
            ?? throw new InvalidOperationException("Migration smoke has no destination box.");
        var creature = source.Session.GetEntities().FirstOrDefault(entity =>
            string.Equals(entity.TryGetData<string>(DemoKeys.Kind) is { found: true } kind ? kind.value : null,
                DemoKeys.CreatureKind, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Migration smoke has no source creature.");
        var genome = creature.TryGetData<string>(DemoKeys.BodyGenome) is { found: true } data
            ? data.value
            : throw new InvalidOperationException("Source creature has no genome.");
        var result = BoxMigration.Migrate(_entry.Session, source.Id, creature.Name, target.Id);
        if (!result.Success || result.MigrantName is null)
            throw new InvalidOperationException($"Migration failed: {result.Reason}.");
        var migrant = target.Session.FindByName(result.MigrantName)
            ?? throw new InvalidOperationException("Migrated creature is missing in the destination.");
        if (migrant.TryGetData<string>(DemoKeys.BodyGenome) is not { found: true } migrated
            || !string.Equals(migrated.value, genome, StringComparison.Ordinal))
            throw new InvalidOperationException("Migrated genome changed in transit.");
        if (migrant.TryGetData<string>(DemoKeys.RootOriginBox) is not { found: true } origin
            || !string.Equals(origin.value, source.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("Migrated root origin was not preserved.");
        var same = BoxMigration.Migrate(_entry.Session, source.Id, creature.Name, source.Id);
        if (same.Success)
            throw new InvalidOperationException("Same-box migration must be rejected.");
        _migrationMigrantName = result.MigrantName;
        _migrationGenome = genome;
        _migrationSourceBox = source.Id;
        GD.Print($"DEMO_MIGRATION_OK from={source.Id} to={target.Id} name={result.MigrantName}");
    }

    private void ValidateMigratedAfterLoad()
    {
        if (_migrationMigrantName is null || _migrationGenome is null || _migrationSourceBox is null)
            throw new InvalidOperationException("Migration smoke state is missing.");
        var target = _entry.Session.TryGetBox("meadow")
            ?? throw new InvalidOperationException("Migration smoke lost the destination box.");
        var migrant = target.Session.FindByName(_migrationMigrantName)
            ?? throw new InvalidOperationException("Migrated creature did not survive save/load.");
        if (migrant.TryGetData<string>(DemoKeys.BodyGenome) is not { found: true } genome
            || !string.Equals(genome.value, _migrationGenome, StringComparison.Ordinal))
            throw new InvalidOperationException("Migrated genome changed across save/load.");
        if (migrant.TryGetData<string>(DemoKeys.RootOriginBox) is not { found: true } origin
            || !string.Equals(origin.value, _migrationSourceBox, StringComparison.Ordinal))
            throw new InvalidOperationException("Migrated root origin changed across save/load.");
        GD.Print($"DEMO_MIGRATION_RESTORED name={_migrationMigrantName}");
    }

    private void ValidateBoxTopology()
    {
        var sessions = _entry.Runtime.SessionManager;
        var boxes = _entry.Session.Boxes
            .Where(box => box.IsForeground
                ? ReferenceEquals(sessions.ForegroundSession, box.Session)
                : ReferenceEquals(sessions.TryGet(box.LevelId), box.Session))
            .ToArray();
        if (boxes.Length < 2)
            throw new InvalidOperationException(
                $"Multi-box smoke expected 2 live boxes, found {boxes.Length}; " +
                $"registered={string.Join(',', _entry.Session.Boxes.Select(box => box.LevelId))}; " +
                $"sessions={string.Join(',', sessions.Keys)}.");
        foreach (var box in boxes)
        {
            var controller = box.Controller
                ?? throw new InvalidOperationException($"Box '{box.Id}' has no controller.");
            var time = controller.TryGetData<float>(DemoKeys.Time) is { found: true } value ? value.value : 0f;
            if (time <= 0f)
                throw new InvalidOperationException($"Box '{box.Id}' did not advance time.");
        }

        GD.Print($"DEMO_BOXES count={boxes.Length} " + string.Join(' ', boxes.Select(box =>
            $"{box.Id}:{box.Session.GetEntities().Count}")));
    }

    private void AssertSaveWrittenByThisRun(string slot)
    {
        var saveRoot = ProjectSettings.GlobalizePath(DemoEntry.SaveRoot);
        var slotRoot = Path.Combine(saveRoot, $"save_{slot}");
        var scene = (Directory.Exists(slotRoot)
            ? Directory.GetFiles(slotRoot, "snd_scene.json", SearchOption.AllDirectories).FirstOrDefault()
            : null)
            ?? throw new InvalidOperationException($"Save '{slot}' has no scene file.");
        if (File.GetLastWriteTimeUtc(scene) < _runStartedUtc.AddSeconds(-1d))
            throw new InvalidOperationException($"Save '{slot}' was not written by this run.");
    }

    private int VerifyIterateCheckpoints()
    {
        var marks = IterateCheckpointSchedule.Marks(
            _fastForwardStartTime, _fastForwardTarget, _checkpointInterval);
        if (marks.Count == 0)
        {
            _verifiedCheckpoints = 0;
            return 0;
        }

        var saves = _entry.Context.Save.ListSaves();
        foreach (var mark in marks)
        {
            var slot = DemoUi.IterateCheckpointSlot(mark);
            if (!saves.Contains(slot, StringComparer.Ordinal))
                throw new InvalidOperationException($"Iterate checkpoint save '{slot}' is missing.");
            AssertSaveWrittenByThisRun(slot);
        }

        _verifiedCheckpoints = marks.Count;
        GD.Print($"DEMO_ITERATE_CHECKPOINTS count={marks.Count} " +
            $"first={DemoUi.IterateCheckpointSlot(marks[0])} last={DemoUi.IterateCheckpointSlot(marks[^1])}");
        return marks.Count;
    }
}
