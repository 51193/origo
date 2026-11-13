using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Save.Meta;
using Origo.Core.Snd;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo;

/// <summary>
///     All demo UI, built programmatically to keep the sample a single scene
///     and free of external assets. The UI talks to SND entities through public
///     context/session APIs; no framework internals are touched.
/// </summary>
public partial class DemoUi : CanvasLayer
{
    private enum PendingWork
    {
        None,
        NewGameAfterReload,
        LoadAfterRequest,
        SaveAfterRequest,
    }

    private static readonly Color TextColor = new(0.9f, 0.95f, 0.97f);
    private static readonly Color MutedColor = new(0.58f, 0.68f, 0.72f);
    private static readonly Color AccentColor = new(0.36f, 0.9f, 0.76f);
    private static readonly Color PanelColor = new(0.035f, 0.07f, 0.1f, 0.96f);
    private static readonly Color ShadeColor = new(0.0f, 0.01f, 0.02f, 0.72f);

    private readonly DemoEntry _entry;
    private Control _root = null!;
    private ColorRect _shade = null!;
    private PanelContainer _menuPanel = null!;
    private PanelContainer _newGamePanel = null!;
    private PanelContainer _loadPanel = null!;
    private PanelContainer _savePanel = null!;
    private PanelContainer _pausePanel = null!;
    private PanelContainer _extinctPanel = null!;
    private PanelContainer _hud = null!;
    private PanelContainer _inspector = null!;
    private PanelContainer _diversityPanel = null!;
    private PanelContainer _advancedPanel = null!;
    private PanelContainer _fastForwardPanel = null!;
    private ColorRect _fastForwardOverlay = null!;
    private ProgressBar _fastForwardProgress = null!;
    private Label _fastForwardStatus = null!;
    private LineEdit _fastForwardSeconds = null!;

    private Label _hudLabel = null!;
    private Label _hintLabel = null!;
    private Label _toastLabel = null!;
    private Label _loadingLabel = null!;
    private Label _inspectorTitle = null!;
    private Label _inspectorStats = null!;
    private Label _inspectorSensors = null!;
    private Label _inspectorGenome = null!;
    private Label _inspectorLook = null!;
    private ColorRect _inspectorSwatch = null!;
    private Label _diversityIndex = null!;
    private Label _diversityStats = null!;
    private Label _diversityPatterns = null!;
    private DiversitySparkline _diversityChart = null!;
    private VBoxContainer _diversityThemeRows = null!;
    private VBoxContainer _diversityLineageRows = null!;
    private Label _loadStatus = null!;
    private Label _saveStatus = null!;

    private VBoxContainer _loadList = null!;
    private VBoxContainer _saveList = null!;
    private Button _continueButton = null!;
    private Button _pauseButton = null!;
    private Button _feedButton = null!;
    private LineEdit _seedInput = null!;
    private OptionButton _mutationOption = null!;
    private OptionButton _foodOption = null!;
    private OptionButton _collisionOption = null!;
    private OptionButton _bloomOption = null!;
    private ConfirmationDialog _confirmDialog = null!;
    private DemoSettings _settingsDraft = DemoSettings.Defaults();
    private DemoSettings _pendingSettings = DemoSettings.Defaults();
    private readonly List<(HSlider Slider, LineEdit Edit, Func<DemoSettings, float> Read)> _advancedNumbers = [];
    private readonly List<(CheckButton Toggle, Func<DemoSettings, bool> Read)> _advancedToggles = [];

    private bool _ready;
    private bool _gameVisible;
    private bool _panning;
    private bool _busy;
    private PendingWork _pending;
    private int _pendingSeed;
    private double _pendingMutation;
    private int _pendingFoodPreset;
    private bool _pendingCollision;
    private bool _pendingBloom;
    private double _autoSaveTimer;
    private double _wallSeconds;
    private double _toastTimer;
    private double _feedCooldown;
    private double _hudRefreshTimer;
    private double _realTimeWriteTimer;
    private double _gameplayProfileTimer;
    private bool _fastForwarding;
    private int _previousMaxFps = 60;
    private DisplayServer.VSyncMode _previousVsync = DisplayServer.VSyncMode.Enabled;
    private ulong _fastForwardStartWallMs;
    private ulong _fastForwardLastWallMs;
    private float _fastForwardLastSimTime;
    private double _fastForwardInstantRate;
    private double _diversityTimer;
    private string? _selectedName;
    private string _pendingSaveSlot = string.Empty;
    private ISessionRun? _cachedSession;
    private ISndEntity? _cachedController;
    private ISessionRun? _cachedInspectSession;
    private string? _cachedInspectName;
    private ISndEntity? _cachedInspectEntity;

    public DemoUi(DemoEntry entry)
    {
        _entry = entry;
        _settingsDraft = DemoSettings.Current.Clone();
        _pendingSettings = _settingsDraft.Clone();
    }

    public override void _Ready()
    {
        Layer = 5;
        BuildUi();
    }

    public override void _Process(double delta)
    {
        if (!_ready)
        {
            TryInitialize();
            return;
        }

        if (_busy)
        {
            TickPending();
            return;
        }

        if (_fastForwarding)
        {
            UpdateFastForward(delta);
            return;
        }

        if (_gameVisible)
        {
            _wallSeconds += delta;
            _realTimeWriteTimer += delta;
            if (_realTimeWriteTimer >= 1d)
            {
                _realTimeWriteTimer = 0d;
                PersistRealSeconds();
            }

            TickGame(delta);
            _gameplayProfileTimer += delta;
            if (_gameplayProfileTimer >= 15d)
            {
                _gameplayProfileTimer = 0d;
                _entry.Persistence.FlushProfile();
            }
        }

        _hudRefreshTimer -= delta;
        if (_hudRefreshTimer <= 0d)
        {
            _hudRefreshTimer = 0.15d;
            UpdateHud();
            UpdateInspector();
        }

        UpdateDiversity(delta);
        UpdateToast(delta);
        HandleExtinctionState();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_ready || _busy)
            return;

        if (_fastForwarding)
        {
            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
                FindController()?.SetData(DemoKeys.FastForwardActive, false);
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape)
            {
                if (AnyModalVisible())
                    CloseModals();
                else if (_gameVisible)
                    OpenPause();
                return;
            }

            if (key.Keycode == Key.R && _gameVisible && !AnyModalVisible())
            {
                _entry.WorldView.ResetView();
                return;
            }
        }

        if (!_gameVisible || AnyModalVisible())
            return;

        if (@event is InputEventMouseButton wheel
            && wheel.Pressed
            && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            var factor = wheel.ButtonIndex == MouseButton.WheelUp ? 1.16f : 1f / 1.16f;
            _entry.WorldView.ZoomAt(GetViewport().GetMousePosition(), factor);
            return;
        }

        if (@event is InputEventMouseButton panButton
            && panButton.ButtonIndex is MouseButton.Right or MouseButton.Middle)
        {
            _panning = panButton.Pressed;
            return;
        }

        if (@event is InputEventMouseMotion motion && _panning)
        {
            _entry.WorldView.Pan(motion.Relative);
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;

        var click = _entry.WorldView.ScreenToWorld(GetViewport().GetMousePosition());
        var nearest = FindCreatureNear(click, 24f / _entry.WorldView.ViewScale);
        _selectedName = nearest?.Name;
        _entry.WorldView.SelectedName = _selectedName;
        FindController()?.SetData(DemoKeys.InspectTarget, _selectedName ?? string.Empty);
        if (_selectedName is null)
            _inspector.Visible = false;
        else
            _inspector.Visible = true;
        UpdateInspector();
    }

    private void TryInitialize()
    {
        var context = _entry.Context;
        if (context is null
            || !context.Lifecycle.IsBootstrapCompleted
            || !context.Deferred.IsPersistenceIdle)
            return;

        if (FindController() is null)
            return;

        _ready = true;
        ShowMainMenu();
    }

    /// <summary>Current pond controller for gameplay services.</summary>
    public ISndEntity? GameplayController => FindController();

    /// <summary>Persists the current progression state after a UI unlock.</summary>
    public void PersistGameplayProgression()
    {
        var controller = FindController();
        if (controller is not null)
            _entry.Persistence.Flush(controller);
    }

    /// <summary>
    ///     Resolves and caches the pond controller. The linear name scan runs
    ///     again only after the foreground session changes or the entity is gone.
    /// </summary>
    private ISndEntity? FindController()
    {
        var session = _entry.Runtime.SessionManager.ForegroundSession;
        if (session is null)
        {
            _cachedSession = null;
            _cachedController = null;
            return null;
        }

        if (!ReferenceEquals(session, _cachedSession))
        {
            _cachedSession = session;
            _cachedController = null;
        }

        if (_cachedController is null || _cachedController.IsPendingKill)
            _cachedController = session.FindByName(DemoEntry.ControllerName);
        return _cachedController;
    }

    /// <summary>
    ///     Resolves and caches the inspected creature for the current session and
    ///     selection name; the inspector no longer scans the session per frame.
    /// </summary>
    private ISndEntity? FindSelectedCreature(ISessionRun? session)
    {
        if (session is null || _selectedName is null)
        {
            _cachedInspectSession = null;
            _cachedInspectName = null;
            _cachedInspectEntity = null;
            return null;
        }

        if (!ReferenceEquals(session, _cachedInspectSession)
            || !string.Equals(_selectedName, _cachedInspectName, StringComparison.Ordinal))
        {
            _cachedInspectSession = session;
            _cachedInspectName = _selectedName;
            _cachedInspectEntity = null;
        }

        if (_cachedInspectEntity is null || _cachedInspectEntity.IsPendingKill)
            _cachedInspectEntity = session.FindByName(_selectedName);
        return _cachedInspectEntity;
    }

    private void ShowMainMenu()
    {
        SetGamePaused(true);
        _gameVisible = false;
        _panning = false;
        _selectedName = null;
        _entry.WorldView.SelectedName = null;
        FindController()?.SetData(DemoKeys.InspectTarget, string.Empty);
        HideAllPanels();
        _menuPanel.Visible = true;
        _shade.Visible = true;
        _continueButton.Disabled = _entry.Context.Save.ListSaves().Count == 0;
        _inspector.Visible = false;
    }

    private void ShowGame()
    {
        HideAllPanels();
        _gameVisible = true;
        _shade.Visible = false;
        _hud.Visible = true;
        _hintLabel.Visible = true;
        _autoSaveTimer = 0d;
        _hudRefreshTimer = 0d;
        _entry.WorldView.RefreshWorldBounds();
        UpdateHud();
    }

    private void HideAllPanels()
    {
        _menuPanel.Visible = false;
        _newGamePanel.Visible = false;
        _loadPanel.Visible = false;
        _savePanel.Visible = false;
        _pausePanel.Visible = false;
        _extinctPanel.Visible = false;
        _shade.Visible = false;
        _hud.Visible = false;
        _hintLabel.Visible = false;
        if (_inspector is not null)
            _inspector.Visible = false;
        if (_diversityPanel is not null)
            _diversityPanel.Visible = false;
        if (_advancedPanel is not null)
            _advancedPanel.Visible = false;
        if (_fastForwardPanel is not null)
            _fastForwardPanel.Visible = false;
        if (_loadingLabel is not null)
            _loadingLabel.Visible = false;
    }

    private bool AnyModalVisible() =>
        _menuPanel.Visible || _newGamePanel.Visible || _loadPanel.Visible
        || _savePanel.Visible || _pausePanel.Visible || _extinctPanel.Visible
        || _diversityPanel.Visible || _advancedPanel.Visible
        || _fastForwardPanel.Visible;

    private void CloseModals()
    {
        if (_gameVisible)
        {
            ResumeGame();
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void BackToMenuOrPause()
    {
        if (_gameVisible)
            OpenPause();
        else
            ShowMainMenu();
    }

    private void TickGame(double delta)
    {
        var controller = FindController();
        if (controller is null)
            return;

        var paused = ReadBool(controller, DemoKeys.Paused, false);
        var phase = ReadString(controller, DemoKeys.Phase, DemoKeys.PhaseMenu);
        if (!paused && phase == DemoKeys.PhaseRunning)
        {
            _autoSaveTimer += delta;
            if (_autoSaveTimer >= 60d && _entry.Context.Deferred.IsPersistenceIdle)
            {
                _autoSaveTimer = 0d;
                PersistRealSeconds();
                PersistGameplayProgression();
                _pendingSaveSlot = DemoEntry.AutoSaveId;
                _pending = PendingWork.SaveAfterRequest;
                _busy = true;
                _entry.Context.Save.RequestSaveGame(DemoEntry.AutoSaveId);
            }
        }

        if (_feedCooldown > 0d)
            _feedCooldown = Math.Max(0d, _feedCooldown - delta);
    }

    private void HandleExtinctionState()
    {
        if (!_gameVisible || _busy)
            return;
        var controller = FindController();
        if (controller is null)
            return;
        var phase = ReadString(controller, DemoKeys.Phase, DemoKeys.PhaseMenu);
        if (phase != DemoKeys.PhaseExtinct || _extinctPanel.Visible || AnyModalVisible())
            return;

        SetGamePaused(true);
        HideAllPanels();
        _extinctPanel.Visible = true;
        _shade.Visible = true;
        _hud.Visible = false;
        _hintLabel.Visible = false;
    }

    private void TickPending()
    {
        var context = _entry.Context;
        if (!context.Deferred.IsPersistenceIdle)
            return;

        switch (_pending)
        {
            case PendingWork.NewGameAfterReload:
                ApplyNewGame();
                break;
            case PendingWork.LoadAfterRequest:
                FinishLoad();
                break;
            case PendingWork.SaveAfterRequest:
                context.Save.SetContinueTarget(_pendingSaveSlot);
                ShowToast($"Saved {_pendingSaveSlot}");
                _pending = PendingWork.None;
                _busy = false;
                RebuildSlots(false);
                RebuildSlots(true);
                break;
            default:
                _busy = false;
                break;
        }
    }

    private void ApplyNewGame()
    {
        _pending = PendingWork.None;
        _busy = false;
        var controller = FindController();
        if (controller is null)
        {
            ShowToast("No world controller after reset.", true);
            ShowMainMenu();
            return;
        }

        _settingsDraft.DefaultMutationRate = _pendingMutation;
        _settingsDraft.BodyMutationRate = _pendingMutation;
        _settingsDraft.CollisionEnabled = _pendingCollision;
        _settingsDraft.EarlyFoodBloomEnabled = _pendingBloom;
        _settingsDraft.Normalize();
        DemoSettings.Current = _settingsDraft.Clone();
        _pendingSettings = DemoSettings.Current.Clone();
        controller.SetData(DemoKeys.ConfigJson, DemoSettings.Current.ToJson());
        controller.SetData(DemoKeys.Seed, _pendingSeed);
        controller.SetData(DemoKeys.MutationRate, (float)_pendingMutation);
        controller.SetData(DemoKeys.FoodPreset, _pendingFoodPreset);
        controller.SetData(DemoKeys.CollisionEnabled, _pendingCollision);
        controller.SetData(DemoKeys.BloomEnabled, _pendingBloom);
        controller.SetData(DemoKeys.Speed, 1);
        controller.SetData(DemoKeys.Paused, false);
        controller.SetData(DemoKeys.Phase, DemoKeys.PhaseStarting);
        controller.SetData(DemoKeys.GenomeVersion, 6);
        controller.SetData(DemoKeys.Initialized, false);
        controller.SetData(DemoKeys.Time, 0f);
        controller.SetData(DemoKeys.Tick, 0);
        controller.SetData(DemoKeys.Accumulator, 0f);
        controller.SetData(DemoKeys.FoodAccumulator, 0f);
        controller.SetData(DemoKeys.NextCreatureId, 1);
        controller.SetData(DemoKeys.NextFoodId, 1);
        controller.SetData(DemoKeys.NextLineageId, 1);
        controller.SetData(DemoKeys.MaxGeneration, 0);
        controller.SetData(DemoKeys.FeedRequest, 0);
        _wallSeconds = 0d;
        _realTimeWriteTimer = 0d;
        controller.SetData(DemoKeys.RealSeconds, 0f);
        controller.SetData(DemoKeys.FastForwardActive, false);
        controller.SetData(DemoKeys.FastForwardProgress, 0f);
        FindController()?.SetData(DemoKeys.InspectTarget, string.Empty);
        ShowGame();
        _diversityChart.Clear();
        ShowToast("A new pond is alive.");
    }

    private void FinishLoad()
    {
        _pending = PendingWork.None;
        _busy = false;
        var controller = FindController();
        if (controller is null)
        {
            ShowToast("Load completed without a world controller.", true);
            ShowMainMenu();
            return;
        }

        if (ReadInt(controller, DemoKeys.GenomeVersion, 0) < 6)
        {
            controller.SetData(DemoKeys.Paused, true);
            ShowToast("This save predates genome v6 and is not compatible.", true);
            ShowMainMenu();
            return;
        }

        controller.SetData(DemoKeys.Paused, false);
        var phase = ReadString(controller, DemoKeys.Phase, DemoKeys.PhaseMenu);
        if (phase == DemoKeys.PhaseStarting)
            controller.SetData(DemoKeys.Phase, DemoKeys.PhaseRunning);
        var loadedController = FindController();
        if (loadedController is not null)
        {
            _wallSeconds = ReadFloat(loadedController, DemoKeys.RealSeconds, 0f);
            _realTimeWriteTimer = 0d;
            loadedController.SetData(DemoKeys.FastForwardActive, false);
            loadedController.SetData(DemoKeys.FastForwardProgress, 0f);
            var configJson = ReadString(loadedController, DemoKeys.ConfigJson, string.Empty);
            if (configJson.Length > 0)
                DemoSettings.Current = DemoSettings.FromJson(configJson);
            _settingsDraft = DemoSettings.Current.Clone();
        }

        ShowGame();
        _diversityChart.Clear();
        ShowToast("Pond loaded.");
    }

    private void NewGame()
    {
        var seedText = _seedInput.Text.Trim();
        var seed = int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : Random.Shared.Next(1, int.MaxValue);
        var mutation = _mutationOption.Selected switch
        {
            0 => 0.005d,
            2 => 0.03d,
            _ => 0.015d,
        };
        StartNewGame(
            seed,
            mutation,
            _foodOption.Selected,
            _collisionOption.Selected == 1,
            _bloomOption.Selected == 1);
    }

    private void StartNewGame(int seed, double mutation, int foodPreset, bool collision, bool bloom)
    {
        _pendingSeed = seed;
        _pendingMutation = mutation;
        _pendingFoodPreset = foodPreset;
        _pendingCollision = collision;
        _pendingBloom = bloom;
        _pendingSettings = _settingsDraft.Clone();
        _pendingSettings.DefaultMutationRate = mutation;
        _pendingSettings.BodyMutationRate = mutation;
        _pendingSettings.CollisionEnabled = collision;
        _pendingSettings.EarlyFoodBloomEnabled = bloom;

        var controller = FindController();
        var needsReload = controller is not null
            && (ReadBool(controller, DemoKeys.Initialized, false)
                || _entry.Runtime.SessionManager.ForegroundSession?.GetEntities().Count > 1);
        if (needsReload)
        {
            _pending = PendingWork.NewGameAfterReload;
            _busy = true;
            HideAllPanels();
            _shade.Visible = true;
            ShowToast("Resetting the pond...");
            _entry.Context.Lifecycle.RequestLoadMainMenuEntrySave();
            return;
        }

        ApplyNewGame();
    }

    internal bool IsReadyForSmoke => _ready;

    internal bool IsBusyForSmoke => _busy;

    internal bool IsNewGamePanelVisibleForSmoke => _newGamePanel.Visible;

    internal void CloseSmokePanel() => CloseModals();

    internal bool TryGetVisibleButtonCenter(string text, out Vector2 center)
    {
        foreach (var node in _root.FindChildren("*", "Button", true, false))
        {
            if (node is not Button button
                || !button.IsVisibleInTree()
                || !string.Equals(button.Text, text, StringComparison.Ordinal))
                continue;
            center = button.GlobalPosition + (button.Size / 2f);
            return true;
        }

        center = Vector2.Zero;
        return false;
    }

    internal bool TryGetMenuButtonCenter(string text, out Vector2 center)
    {
        foreach (var node in _menuPanel.FindChildren("*", "Button", true, false))
        {
            if (node is not Button button || !string.Equals(button.Text, text, StringComparison.Ordinal))
                continue;
            center = button.GlobalPosition + (button.Size / 2f);
            return true;
        }

        center = Vector2.Zero;
        return false;
    }

    internal void StartSmokeGame(int seed) =>
        StartNewGame(seed, 0.015d, 1, Tuning.CollisionEnabled, Tuning.EarlyFoodBloomEnabled);

    internal void StartFastForwardForSmoke(float seconds) => StartFastForwardInternal(seconds);

    internal bool IsFastForwardingForSmoke => _fastForwarding;

    internal void SetSmokeSpeed(int speed) => SetSpeed(speed);

    internal void SaveSmokeGame(string id) => SaveTo(id);

    internal void LoadSmokeGame(string id) => LoadSave(id);

    internal (int creatures, int food, int generation, float time, string phase) SmokeStats()
    {
        var controller = FindController();
        var session = _entry.Runtime.SessionManager.ForegroundSession;
        var creatures = 0;
        var food = 0;
        if (session is not null)
        {
            foreach (var entity in session.GetEntities())
            {
                var (found, kind) = entity.TryGetData<string>(DemoKeys.Kind);
                if (!found)
                    continue;
                if (string.Equals(kind, DemoKeys.CreatureKind, StringComparison.Ordinal))
                    creatures++;
                else if (string.Equals(kind, DemoKeys.FoodKind, StringComparison.Ordinal))
                    food++;
            }
        }

        return (
            creatures,
            food,
            controller is null ? 0 : ReadInt(controller, DemoKeys.MaxGeneration, 0),
            controller is null ? 0f : ReadFloat(controller, DemoKeys.Time, 0f),
            controller is null ? "missing" : ReadString(controller, DemoKeys.Phase, "missing"));
    }

    private void TogglePause()
    {
        if (!_gameVisible || _busy)
            return;
        if (AnyModalVisible())
        {
            CloseModals();
            return;
        }

        var controller = FindController();
        var paused = controller is not null && ReadBool(controller, DemoKeys.Paused, false);
        if (paused)
        {
            SetGamePaused(false);
            HideAllPanels();
            _hud.Visible = true;
            _hintLabel.Visible = true;
            _shade.Visible = false;
        }
        else
        {
            SetGamePaused(true);
            HideAllPanels();
            _pausePanel.Visible = true;
            _shade.Visible = true;
            _hud.Visible = false;
            _hintLabel.Visible = false;
        }
    }

    private void ResumeGame()
    {
        SetGamePaused(false);
        HideAllPanels();
        _gameVisible = true;
        _hud.Visible = true;
        _hintLabel.Visible = true;
        _shade.Visible = false;
        if (_selectedName is not null)
            _inspector.Visible = true;
    }

    private void OpenPause()
    {
        if (_busy)
            return;
        SetGamePaused(true);
        HideAllPanels();
        _pausePanel.Visible = true;
        _shade.Visible = true;
        _hud.Visible = false;
        _hintLabel.Visible = false;
    }

    private void OpenNewGame()
    {
        if (_busy)
            return;
        HideAllPanels();
        _newGamePanel.Visible = true;
        _shade.Visible = true;
        _hud.Visible = false;
        _hintLabel.Visible = false;
    }

    private void OpenLoad()
    {
        if (_busy)
            return;
        SetGamePaused(true);
        HideAllPanels();
        _loadPanel.Visible = true;
        _shade.Visible = true;
        _hud.Visible = false;
        _hintLabel.Visible = false;
        RebuildSlots(false);
    }

    private void OpenSave()
    {
        if (_busy)
            return;
        SetGamePaused(true);
        HideAllPanels();
        _savePanel.Visible = true;
        _shade.Visible = true;
        _hud.Visible = false;
        _hintLabel.Visible = false;
        RebuildSlots(true);
    }

    private void SetGamePaused(bool paused)
    {
        var controller = FindController();
        controller?.SetData(DemoKeys.Paused, paused);
    }

    private void ToggleHudPause()
    {
        var controller = FindController();
        var paused = controller is not null && ReadBool(controller, DemoKeys.Paused, false);
        if (paused)
        {
            SetGamePaused(false);
            _hud.Visible = true;
            _hintLabel.Visible = true;
            _shade.Visible = false;
        }
        else
        {
            SetGamePaused(true);
            _hud.Visible = true;
            _hintLabel.Visible = true;
        }
    }

    private void SetSpeed(int speed)
    {
        var controller = FindController();
        if (controller is null)
            return;
        controller.SetData(DemoKeys.Speed, speed);
        controller.SetData(DemoKeys.Paused, false);
        _hud.Visible = true;
        _hintLabel.Visible = true;
        _shade.Visible = false;
    }

    private void Feed()
    {
        var controller = FindController();
        if (controller is null || _feedCooldown > 0d)
            return;
        var request = ReadInt(controller, DemoKeys.FeedRequest, 0);
        controller.SetData(DemoKeys.FeedRequest, request + 1);
        _feedCooldown = Tuning.FeedCooldownSeconds;
        ShowToast("Nutrients dropped.");
    }

    private ISndEntity? FindCreatureNear(Vector2 worldPoint, float radius)
    {
        var session = _entry.Runtime.SessionManager.ForegroundSession;
        if (session is null)
            return null;
        ISndEntity? best = null;
        var bestDistance = radius;
        foreach (var entity in session.GetEntities())
        {
            if (!entity.TryGetData<string>(DemoKeys.Kind).found
                || !string.Equals(entity.TryGetData<string>(DemoKeys.Kind).value, DemoKeys.CreatureKind, StringComparison.Ordinal))
                continue;
            var position = new Vector2(
                ReadFloat(entity, DemoKeys.X, 0f),
                ReadFloat(entity, DemoKeys.Y, 0f));
            var distance = position.DistanceTo(worldPoint);
            if (distance > bestDistance)
                continue;
            bestDistance = distance;
            best = entity;
        }

        return best;
    }

    private void UpdateHud()
    {
        if (!_gameVisible)
            return;
        var controller = FindController();
        if (controller is null)
            return;
        var session = _entry.Runtime.SessionManager.ForegroundSession;
        var creatures = 0;
        var food = 0;
        var lineages = new HashSet<int>();
        if (session is not null)
        {
            foreach (var entity in session.GetEntities())
            {
                var (found, kind) = entity.TryGetData<string>(DemoKeys.Kind);
                if (!found)
                    continue;
                if (string.Equals(kind, DemoKeys.CreatureKind, StringComparison.Ordinal))
                {
                    creatures++;
                    lineages.Add(ReadInt(entity, DemoKeys.Lineage, 1));
                }
                else if (string.Equals(kind, DemoKeys.FoodKind, StringComparison.Ordinal))
                {
                    food++;
                }
            }
        }

        var time = ReadFloat(controller, DemoKeys.Time, 0f);
        var phase = ReadString(controller, DemoKeys.Phase, DemoKeys.PhaseMenu);
        var paused = ReadBool(controller, DemoKeys.Paused, false);
        var speed = ReadInt(controller, DemoKeys.Speed, 1);
        var bloomEnabled = ReadBool(controller, DemoKeys.BloomEnabled, Tuning.EarlyFoodBloomEnabled);
        var simSeconds = (int)MathF.Floor(time);
        var realSeconds = (int)MathF.Floor((float)_wallSeconds);
        var simClock = FormatClock(simSeconds);
        var realClock = FormatClock(realSeconds);
        _hudLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Sim {simClock}  Real {realClock}  Pop {creatures}/{Tuning.PopulationCap}  Gen {ReadInt(controller, DemoKeys.MaxGeneration, 0)}  Lines {lineages.Count}  Food {food}  Spikes {ReadInt(controller, DemoKeys.SpikeHits, 0)} (K {ReadInt(controller, DemoKeys.SpikeKills, 0)})  OG {ReadInt(controller, DemoKeys.OvergrowthCount, 0)}  Carc {ReadInt(controller, DemoKeys.CorpsesCreated, 0)}/{ReadInt(controller, DemoKeys.CarrionFoodReleased, 0)}  Scav {ReadInt(controller, DemoKeys.CorpseMeals, 0)}{BloomSuffix(time, bloomEnabled)}  {(phase == DemoKeys.PhaseExtinct ? "EXTINCT" : paused ? "PAUSED" : $"x{speed}")}");
        _pauseButton.Text = paused ? "Resume" : "Pause";
        _feedButton.Disabled = _feedCooldown > 0d;
        _feedButton.Text = _feedCooldown > 0d
            ? $"Feed ({_feedCooldown:0.0})"
            : "Feed";
    }

    private static string FormatClock(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        return totalSeconds >= 3_600
            ? $"{totalSeconds / 3_600:00}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}"
            : $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private static string BloomSuffix(float time, bool enabled) =>
        enabled && time < Tuning.EarlyFoodRampSeconds ? " · bloom" : string.Empty;

    private void UpdateInspector()
    {
        if (!_gameVisible || _selectedName is null)
            return;
        if (AnyModalVisible())
        {
            _inspector.Visible = false;
            return;
        }
        var session = _entry.Runtime.SessionManager.ForegroundSession;
        var entity = FindSelectedCreature(session);
        if (entity is null)
        {
            _selectedName = null;
            _entry.WorldView.SelectedName = null;
            _inspector.Visible = false;
            return;
        }

        _inspector.Visible = true;
        var lineage = ReadInt(entity, DemoKeys.Lineage, 1);
        var bodyGenome = ReadString(entity, DemoKeys.BodyGenome, string.Empty);
        var genome = CreatureGenome.TryDecode(bodyGenome, out var decoded)
            ? decoded!
            : CreatureGenome.RandomFounder(new Random(0));
        var phenotype = genome.Phenotype;
        var v4Phenotype = GenomeV4Compiler.Compile(genome.Genome, ExpressionContextV4.Neutral);
        var look = GenomeAppearance.Derive(bodyGenome);
        var kills = ReadInt(entity, DemoKeys.Kills, 0);
        var scavenged = ReadInt(entity, DemoKeys.CreatureCorpseMeals, 0);
        var growthPercent = (GrowthScaling.SizeMultiplier(kills, scavenged) - 1f) * 100f;
        _inspectorTitle.Text = $"{_selectedName}   lineage {lineage}";
        _inspectorTitle.AddThemeColorOverride("font_color", ToColor(look.Body));
        _inspectorSwatch.Color = ToColor(look.Body);
        _inspectorLook.Text = $"{look.ThemeName} · {look.PatternName}   {look.Body.ToHex()}";
        _inspectorStats.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Gen {ReadInt(entity, DemoKeys.Generation, 0)}    Energy {ReadFloat(entity, DemoKeys.Energy, 0f):0.0}/{phenotype.MaxEnergy:0}\n" +
            $"Age {ReadFloat(entity, DemoKeys.Age, 0f):0.0}s/{phenotype.MaxAge:0}s    Children {ReadInt(entity, DemoKeys.Children, 0)}\n" +
            $"Kills {kills}    Scavenged {scavenged}    Growth +{growthPercent:0}%\n" +
            $"Size {phenotype.SizeScale:0.00}  Speed {phenotype.MoveSpeed:0}  Sense {phenotype.SenseRange:0}  Spike {phenotype.SpikeRadius:0}\n" +
            $"Battery {phenotype.SpikeBatteryOrgan}  Count {phenotype.SpikeCount}  Spread {phenotype.SpikeSpreadRadians * 180f / MathF.PI:0}°  Jitter {phenotype.SpikeJitterRadians * 180f / MathF.PI:0}°\n" +
            $"Outputs  {ReadString(entity, DemoKeys.Outputs, "—")}\n" +
            $"Sensors  {ReadString(entity, DemoKeys.Sensors, "—")}");
        _inspectorGenome.Text = $"V4 genome: {genome.GeneCount} genes, {v4Phenotype.Brain.Nodes.Count} neural nodes, " +
            $"{v4Phenotype.Brain.Edges.Count} synapses, {v4Phenotype.Body.Organs.Count} organs\n" +
            FormatGenome(genome.Body);
        _inspectorSensors.Text =
            PhenotypeInspectorV4.Inspect(v4Phenotype).Describe();
    }


    private static string FormatGenome(string genome)
    {
        var groups = new List<string>();
        for (var i = 0; i < genome.Length; i += 4)
            groups.Add(genome.Substring(i, Math.Min(4, genome.Length - i)));
        return string.Join(" ", groups);
    }

    /// <summary>Shows a transient HUD toast.</summary>
    public void ShowToast(string message, bool error = false)
    {
        _toastLabel.Text = message;
        _toastLabel.AddThemeColorOverride("font_color", error ? new Color(1f, 0.55f, 0.5f) : AccentColor);
        _toastLabel.Visible = true;
        _toastTimer = 3.2d;
    }

    private void UpdateToast(double delta)
    {
        if (_toastTimer <= 0d)
        {
            _toastLabel.Visible = false;
            return;
        }

        _toastTimer -= delta;
        if (_toastTimer <= 0d)
            _toastLabel.Visible = false;
    }

    private void Confirm(string message, Action action)
    {
        _confirmDialog.Title = "Confirm";
        _confirmDialog.DialogText = message;

        void Unhook()
        {
            _confirmDialog.Confirmed -= OnConfirmed;
            _confirmDialog.Canceled -= OnCanceled;
        }

        void OnConfirmed()
        {
            Unhook();
            action();
        }

        void OnCanceled() => Unhook();

        _confirmDialog.Confirmed += OnConfirmed;
        _confirmDialog.Canceled += OnCanceled;
        _confirmDialog.PopupCentered(new Vector2I(420, 180));
    }

    private void OpenFastForward()
    {
        if (_busy)
            return;
        var controller = FindController();
        var now = controller is null ? 0f : ReadFloat(controller, DemoKeys.Time, 0f);
        _fastForwardSeconds.Text = (now + 60f).ToString("0", CultureInfo.InvariantCulture);
        HideAllPanels();
        _fastForwardPanel.Visible = true;
        _shade.Visible = true;
        _hud.Visible = false;
        _hintLabel.Visible = false;
    }

    private void BuildFastForwardPanel()
    {
        _fastForwardPanel = CenterPanel(540f, 360f);
        var column = VBox(_fastForwardPanel);
        column.AddChild(Title("TIMED ITERATION", 26));
        var note = LabelText(
            "Run the current pond forward to an absolute simulation time. The same simulation ticks and deferred entity work are used; rendering is hidden until the target is reached.",
            MutedColor);
        note.AutowrapMode = TextServer.AutowrapMode.Word;
        column.AddChild(note);
        column.AddChild(LabelText("Target simulation time (seconds)", MutedColor));
        _fastForwardSeconds = new LineEdit { Text = "60", PlaceholderText = "e.g. 3600" };
        column.AddChild(_fastForwardSeconds);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var start = new Button { Text = "Start", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        start.Pressed += StartFastForward;
        row.AddChild(start);
        var cancel = new Button { Text = "Cancel", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cancel.Pressed += CloseModals;
        row.AddChild(cancel);
        column.AddChild(row);
        column.AddChild(LabelText("Escape cancels an active iteration.", MutedColor));

        _fastForwardOverlay = new ColorRect
        {
            Color = new Color(0.01f, 0.02f, 0.035f, 0.98f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _root.AddChild(_fastForwardOverlay);
        _fastForwardOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _fastForwardOverlay.SetOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _fastForwardOverlay.AddChild(center);
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.SetOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 12));
        center.AddChild(panel);
        var panelMargin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            panelMargin.AddThemeConstantOverride(side, 26);
        panel.AddChild(panelMargin);
        var overlayColumn = new VBoxContainer();
        overlayColumn.AddThemeConstantOverride("separation", 14);
        overlayColumn.CustomMinimumSize = new Vector2(560f, 0f);
        panelMargin.AddChild(overlayColumn);
        var title = new Label { Text = "ITERATING POND", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        overlayColumn.AddChild(title);
        _fastForwardStatus = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = MutedColor };
        _fastForwardStatus.AddThemeFontSizeOverride("font_size", 13);
        overlayColumn.AddChild(_fastForwardStatus);
        _fastForwardProgress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = true,
            CustomMinimumSize = new Vector2(0f, 26f),
        };
        overlayColumn.AddChild(_fastForwardProgress);
        var hint = LabelText("Simulation is running at maximum CPU speed.", MutedColor);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        overlayColumn.AddChild(hint);
    }

    private void StartFastForward()
    {
        if (!float.TryParse(
                _fastForwardSeconds.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var target))
            return;
        StartFastForwardInternal(target);
    }

    private void StartFastForwardInternal(float target)
    {
        var controller = FindController();
        if (controller is null
            || !string.Equals(ReadString(controller, DemoKeys.Phase, string.Empty), DemoKeys.PhaseRunning, StringComparison.Ordinal))
            return;

        var now = ReadFloat(controller, DemoKeys.Time, 0f);
        if (target <= now + 0.05f)
        {
            ShowToast("Target time must be greater than the current simulation time.");
            return;
        }

        target = Mathf.Clamp(target, now + 0.1f, 86_400f);
        controller.SetData(DemoKeys.FastForwardStart, now);
        controller.SetData(DemoKeys.FastForwardTarget, target);
        controller.SetData(DemoKeys.FastForwardProgress, 0f);
        controller.SetData(DemoKeys.FastForwardActive, true);
        controller.SetData(DemoKeys.Paused, false);
        controller.SetData(DemoKeys.Accumulator, 0f);

        HideAllPanels();
        _shade.Visible = false;
        _hud.Visible = false;
        _hintLabel.Visible = false;
        _inspector.Visible = false;
        _entry.WorldView.Visible = false;
        _fastForwardOverlay.Visible = true;
        _fastForwardProgress.Value = 0;
        _fastForwardStatus.Text = $"Start {now:0.0}  →  target {target:0.0}";
        _fastForwardStartWallMs = Time.GetTicksMsec();
        _fastForwardLastWallMs = _fastForwardStartWallMs;
        _fastForwardLastSimTime = now;
        _fastForwardInstantRate = 0d;
        _fastForwarding = true;
        _previousMaxFps = Engine.MaxFps;
        _previousVsync = DisplayServer.WindowGetVsyncMode();
        Engine.MaxFps = 0;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
    }

    private void UpdateFastForward(double delta)
    {
        _wallSeconds += delta;
        _realTimeWriteTimer += delta;
        if (_realTimeWriteTimer >= 1d)
        {
            _realTimeWriteTimer = 0d;
            PersistRealSeconds();
        }

        var controller = FindController();
        if (controller is null)
        {
            FinishFastForward();
            return;
        }

        var active = ReadBool(controller, DemoKeys.FastForwardActive, false);
        var progress = ReadFloat(controller, DemoKeys.FastForwardProgress, 0f);
        var now = ReadFloat(controller, DemoKeys.Time, 0f);
        var target = ReadFloat(controller, DemoKeys.FastForwardTarget, now);
        var start = ReadFloat(controller, DemoKeys.FastForwardStart, now);
        var wallNow = Time.GetTicksMsec();
        var globalWindow = Math.Max(1d, wallNow - _fastForwardStartWallMs) / 1000d;
        var instantWindow = (wallNow - _fastForwardLastWallMs) / 1000d;
        var globalRate = (now - start) / globalWindow;
        if (instantWindow >= 0.1d)
        {
            _fastForwardInstantRate = (now - _fastForwardLastSimTime) / instantWindow;
            _fastForwardLastWallMs = wallNow;
            _fastForwardLastSimTime = now;
        }

        _fastForwardProgress.Value = Mathf.Clamp(progress * 100f, 0f, 100f);
        _fastForwardStatus.Text = $"Sim {now:0.0} / {target:0.0}    Global {globalRate:0.0}×    Instant {_fastForwardInstantRate:0.0}×";
        if (!active)
        {
            var finalTarget = target;
            var finalNow = now;
            FinishFastForward();
            ShowToast($"Iteration complete at sim {finalNow:0.0} (target {finalTarget:0.0}).");
        }
    }

    private void FinishFastForward()
    {
        if (!_fastForwarding)
            return;
        _fastForwarding = false;
        _fastForwardOverlay.Visible = false;
        _entry.WorldView.Visible = true;
        Engine.MaxFps = _previousMaxFps;
        DisplayServer.WindowSetVsyncMode(_previousVsync);
        _shade.Visible = false;
        if (_gameVisible)
        {
            _hud.Visible = true;
            _hintLabel.Visible = true;
            if (_selectedName is not null)
                _inspector.Visible = true;
        }

        _hudRefreshTimer = 0;
        PersistGameplayProgression();
        UpdateHud();
    }

    private PanelContainer CenterPanel(float width, float height)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, height),
        };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 14));
        var center = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.AddChild(center);
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.SetOffsetsPreset(Control.LayoutPreset.FullRect);
        center.AddChild(panel);
        return panel;
    }

    private static VBoxContainer VBox(Control owner)
    {
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right" })
            margin.AddThemeConstantOverride(side, 22);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        owner.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    private static Label Title(string text, int size)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", TextColor);
        return label;
    }

    private static Label LabelText(string text, Color color)
    {
        var label = new Label { Text = text, Modulate = color };
        label.AddThemeFontSizeOverride("font_size", 14);
        return label;
    }

    private static Control Spacer(float height) => new Control { CustomMinimumSize = new Vector2(0f, height) };

    private static Button WideButton(string text)
    {
        var button = MakeButton(text, 0);
        button.CustomMinimumSize = new Vector2(0f, 40f);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return button;
    }

    private static Button MakeButton(string text, float width)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 34f),
        };
        button.AddThemeFontSizeOverride("font_size", 14);
        return button;
    }

    private static StyleBoxFlat PanelStyle(Color color, int radius) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderColor = new Color(0.2f, 0.45f, 0.5f, 0.45f),
    };

    private static string ReadString(ISndEntity entity, string key, string fallback)
    {
        var (found, value) = entity.TryGetData<string>(key);
        return found && value is not null ? value : fallback;
    }

    private static int ReadInt(ISndEntity entity, string key, int fallback)
    {
        var (found, value) = entity.TryGetData<int>(key);
        return found ? value : fallback;
    }

    private static float ReadFloat(ISndEntity entity, string key, float fallback)
    {
        var (found, value) = entity.TryGetData<float>(key);
        return found ? value : fallback;
    }

    private static bool ReadBool(ISndEntity entity, string key, bool fallback)
    {
        var (found, value) = entity.TryGetData<bool>(key);
        return found ? value : fallback;
    }
}
