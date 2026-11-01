using System;
using System.Linq;
using Godot;
using Origo.EvolutionDemo.Logic;

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
    }

    public override void _Process(double delta)
    {
        try
        {
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
                ui.StartFastForwardForSmoke(3f);
                _state = 6;
                _frame = 0;
                break;

            case 6:
                _frame++;
                if (_frame > StartupFrameLimit * 4)
                    throw new InvalidOperationException("Timed iteration did not complete.");
                if (ui.IsFastForwardingForSmoke)
                    break;
                var fastForwarded = ui.SmokeStats();
                if (fastForwarded.time < _fastForwardStartTime + 2f)
                    throw new InvalidOperationException("Timed iteration did not advance simulation time.");
                GD.Print($"DEMO_SMOKE_OK population={_restoredCreatures} saved_population={_savedPopulation} " +
                    $"generation={_restoredGeneration} time={_restoredTime:0.0} food={_restoredFood} " +
                    $"reset_population={_resetPopulation} fast_forward_time={fastForwarded.time:0.0}");
                GetTree().Quit(0);
                break;
        }
    }
}
