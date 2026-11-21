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
    private readonly bool _campaignAutoplay = string.Equals(
        OS.GetEnvironment("ORIGO_DEMO_CAMPAIGN_AUTOPLAY"), "1", StringComparison.Ordinal);
    private static readonly string[] CampaignPurchaseOrder =
    [
        CampaignUnlockIds.ToughFood, CampaignUnlockIds.HardDigest,
        CampaignUnlockIds.Pond1, CampaignUnlockIds.Sprint,
        CampaignUnlockIds.ToxicFood, CampaignUnlockIds.ToxinResistance,
        CampaignUnlockIds.CreatureSense, CampaignUnlockIds.Rhythm,
        CampaignUnlockIds.CorpseCycle, CampaignUnlockIds.CarrionSense,
        CampaignUnlockIds.Pond2, CampaignUnlockIds.Predation,
        CampaignUnlockIds.Battery, CampaignUnlockIds.Armor,
        CampaignUnlockIds.Pond3, CampaignUnlockIds.FoodTypeVision,
        CampaignUnlockIds.Enrichment, CampaignUnlockIds.Pond4,
        CampaignUnlockIds.Pond5, CampaignUnlockIds.Pond6,
    ];
    private int _verifiedCheckpoints;
    private readonly string? _configurationError;
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
    }

    public override void _Process(double delta)
    {
        try
        {
            if (_configurationError is not null)
                throw new InvalidOperationException(_configurationError);
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

        if (_campaignAutoplay && _entry.Session.Runtime.Progression.Mode == GameplayMode.Campaign)
            TryCampaignAutoplay();

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
                        ui.StartSmokeGame(_smokeSeed);
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
                ui.StartSmokeGame(_smokeSeed);
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
                if (_spedUp && running.time < _targetSeconds && running.creatures > 0)
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
                    throw new InvalidOperationException("The smoke run did not reach a second generation.");
                if (restored.time + 0.01f < _savedTime)
                    throw new InvalidOperationException(
                        $"Loaded time regressed: saved={_savedTime:0.0}, restored={restored.time:0.0}.");
                _restoredCreatures = restored.creatures;
                _restoredGeneration = restored.generation;
                _restoredTime = restored.time;
                _restoredFood = restored.food;
                ui.SetSmokeCheckpointInterval(_checkpointInterval);
                ui.StartSmokeGame(_smokeSeed);
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
                if (!string.IsNullOrWhiteSpace(_benchmarkSaveSlot))
                {
                    ui.SaveSmokeGame(_benchmarkSaveSlot);
                    _savedPopulation = fastForwarded.creatures;
                    _savedTime = fastForwarded.time;
                    _state = 7;
                    _frame = 0;
                    break;
                }

                PrintCampaignSummary();
                GD.Print($"DEMO_SMOKE_OK population={_restoredCreatures} saved_population={_savedPopulation} " +
                    $"generation={_restoredGeneration} time={_restoredTime:0.0} food={_restoredFood} " +
                    $"reset_population={_resetPopulation} fast_forward_time={fastForwarded.time:0.0} " +
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
                PrintCampaignSummary();
                GD.Print($"DEMO_BENCHMARK_OK population={_savedPopulation} time={_savedTime:0.0} " +
                    $"slot={_benchmarkSaveSlot} checkpoints={_verifiedCheckpoints}");
                GetTree().Quit(0);
                break;
        }
    }

    private void PrintCampaignSummary()
    {
        if (!_campaignAutoplay)
            return;
        var runtime = _entry.Session.Runtime;
        GD.Print($"DEMO_CAMPAIGN points={runtime.Progression.Points} " +
            $"milestones={runtime.Progression.CompletedMilestones.Count} " +
            $"unlocks={runtime.Progression.UnlockedIds.Count} " +
            $"mode={runtime.Progression.Mode}");
    }

    private void TryCampaignAutoplay()
    {
        var runtime = _entry.Session.Runtime;
        foreach (var unlockId in CampaignPurchaseOrder)
            runtime.TryUnlock(unlockId);
        _entry.Ui.RefreshCampaignWorld();
        AssertCameraFit();
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
        var saveRoot = ProjectSettings.GlobalizePath(DemoEntry.SaveRoot);
        foreach (var mark in marks)
        {
            var slot = DemoUi.IterateCheckpointSlot(mark);
            if (!saves.Contains(slot, StringComparer.Ordinal))
                throw new InvalidOperationException($"Iterate checkpoint save '{slot}' is missing.");
            var slotRoot = Path.Combine(saveRoot, $"save_{slot}");
            var scene = (Directory.Exists(slotRoot)
                ? Directory.GetFiles(slotRoot, "snd_scene.json", SearchOption.AllDirectories).FirstOrDefault()
                : null)
                ?? throw new InvalidOperationException($"Iterate checkpoint save '{slot}' has no scene file.");
            if (File.GetLastWriteTimeUtc(scene) < _runStartedUtc.AddSeconds(-1d))
                throw new InvalidOperationException(
                    $"Iterate checkpoint save '{slot}' was not written by this run.");
        }

        _verifiedCheckpoints = marks.Count;
        GD.Print($"DEMO_ITERATE_CHECKPOINTS count={marks.Count} " +
            $"first={DemoUi.IterateCheckpointSlot(marks[0])} last={DemoUi.IterateCheckpointSlot(marks[^1])}");
        return marks.Count;
    }
}
