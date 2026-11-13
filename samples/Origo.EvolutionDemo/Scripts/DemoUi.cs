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
            GameplayStateSync.Save(controller, GameplayRuntime.EnsureCurrent());
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

    private void LoadSave(string saveId)
    {
        if (_busy)
            return;
        SetGamePaused(true);
        _pending = PendingWork.LoadAfterRequest;
        _busy = true;
        HideAllPanels();
        _shade.Visible = true;
        ShowToast($"Loading {saveId}...");
        try
        {
            _entry.Context.Save.RequestLoadGame(saveId);
        }
        catch (Exception ex)
        {
            _busy = false;
            _pending = PendingWork.None;
            ShowToast($"Load failed: {ex.Message}", true);
            ShowMainMenu();
        }
    }

    private void PersistRealSeconds()
    {
        var controller = FindController();
        if (controller is not null)
            controller.SetData(DemoKeys.RealSeconds, (float)_wallSeconds);
    }

    private void SaveTo(string saveId)
    {
        if (_busy)
            return;
        SetGamePaused(true);
        PersistRealSeconds();
        _pendingSaveSlot = saveId;
        _pending = PendingWork.SaveAfterRequest;
        _busy = true;
        try
        {
            _entry.Context.Save.RequestSaveGame(saveId);
            ShowToast($"Saving {saveId}...");
        }
        catch (Exception ex)
        {
            _busy = false;
            _pending = PendingWork.None;
            ShowToast($"Save failed: {ex.Message}", true);
        }
    }

    private void ContinueGame()
    {
        var saves = _entry.Context.Save.ListSaves();
        if (saves.Count == 0)
            return;
        var id = saves.Contains(DemoEntry.AutoSaveId, StringComparer.Ordinal)
            ? DemoEntry.AutoSaveId
            : saves[^1];
        LoadSave(id);
    }

    private void DeleteSave(string saveId)
    {
        if (_busy)
            return;
        try
        {
            _entry.Context.Save.DeleteSave(saveId);
            RebuildSlots(false);
            RebuildSlots(true);
            ShowToast($"Deleted {saveId}");
        }
        catch (Exception ex)
        {
            ShowToast($"Delete failed: {ex.Message}", true);
        }
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

    private void RebuildSlots(bool saveMode)
    {
        if (!_ready || _entry.Context is null)
            return;
        var list = saveMode ? _saveList : _loadList;
        foreach (var child in list.GetChildren())
            child.QueueFree();
        var status = saveMode ? _saveStatus : _loadStatus;

        IReadOnlyList<SaveMetaDataEntry> entries;
        try
        {
            entries = _entry.Context.Save.ListSavesWithMetaData();
        }
        catch (Exception ex)
        {
            status.Text = $"Could not read saves: {ex.Message}";
            return;
        }

        var byId = entries.ToDictionary(static entry => entry.SaveId, StringComparer.Ordinal);
        var slots = new List<string> { DemoEntry.AutoSaveId, "slot_1", "slot_2", "slot_3" };
        foreach (var existing in entries)
        {
            if (!slots.Contains(existing.SaveId, StringComparer.Ordinal))
                slots.Add(existing.SaveId);
        }

        foreach (var slot in slots)
        {
            var hasSave = byId.TryGetValue(slot, out var entry);
            list.AddChild(BuildSlotRow(slot, hasSave, entry, saveMode));
        }

        status.Text = entries.Count == 0 ? "No saves yet." : $"{entries.Count} save(s) on disk.";
    }

    private PanelContainer BuildSlotRow(string slot, bool hasSave, SaveMetaDataEntry? entry, bool saveMode)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.06f, 0.12f, 0.16f, 0.92f), 6));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);

        var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(info);
        var title = new Label { Text = slot == DemoEntry.AutoSaveId ? "AUTO SAVE" : slot.ToUpperInvariant() };
        title.AddThemeFontSizeOverride("font_size", 16);
        info.AddChild(title);
        var compatible = !hasSave || (entry is not null
            && entry.MetaData.TryGetValue("demo.genomeVersion", out var version)
            && string.Equals(version, "2", StringComparison.Ordinal));
        var details = new Label
        {
            Text = !hasSave
                ? "Empty slot"
                : DescribeSave(entry!) + (compatible ? string.Empty : "\nINCOMPATIBLE: pre-genome-v2 save"),
            Modulate = hasSave && compatible ? Colors.White : MutedColor,
        };
        details.AddThemeFontSizeOverride("font_size", 13);
        info.AddChild(details);

        if (hasSave)
        {
            var load = MakeButton(compatible ? "Load" : "Incompatible", 100);
            load.Disabled = !compatible;
            load.Pressed += () => LoadSave(slot);
            row.AddChild(load);
        }

        if (hasSave && slot != DemoEntry.AutoSaveId)
        {
            var delete = MakeButton("Delete", 80);
            delete.Pressed += () => Confirm($"Delete save {slot}?", () => DeleteSave(slot));
            row.AddChild(delete);
        }

        if (saveMode && slot != DemoEntry.AutoSaveId)
        {
            var save = MakeButton("Save", 80);
            save.Pressed += () => Confirm($"Overwrite {slot}?", () => SaveTo(slot));
            row.AddChild(save);
        }

        if (!hasSave && slot == DemoEntry.AutoSaveId && !saveMode)
            details.Text = "Created automatically every 60 seconds.";

        return panel;
    }

    private static string DescribeSave(SaveMetaDataEntry entry)
    {
        var meta = entry.MetaData;
        string Read(string key, string fallback) => meta.TryGetValue(key, out var value) ? value : fallback;
        var time = Read("demo.time", "0");
        var seconds = double.TryParse(time, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
        var version = Read("demo.genomeVersion", "legacy");
        return $"gen {Read("demo.generation", "?")} · pop {Read("demo.population", "?")} · " +
            $"lines {Read("demo.lineages", "?")} · {Read("demo.mutation", "?")} · genome {version}\n" +
            $"pond time {seconds / 60d:00}:{seconds % 60d:00} · seed {Read("demo.seed", "?")} · {Read("demo.saved", "?")}";
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

    private void BuildUi()
    {
        _root = new Control
        {
            Name = "UiRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_root);
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.SetOffsetsPreset(Control.LayoutPreset.FullRect);

        _shade = new ColorRect
        {
            Color = ShadeColor,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _root.AddChild(_shade);
        _shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _shade.SetOffsetsPreset(Control.LayoutPreset.FullRect);

        BuildHud();
        BuildInspector();
        BuildDiversityPanel();
        BuildAdvancedPanel();
        BuildFastForwardPanel();
        BuildMenu();
        BuildNewGame();
        BuildLoadPanel();
        BuildSavePanel();
        BuildPausePanel();
        BuildExtinctPanel();

        _toastLabel = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _toastLabel.AddThemeFontSizeOverride("font_size", 18);
        _toastLabel.AddThemeConstantOverride("outline_size", 5);
        _toastLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _root.AddChild(_toastLabel);
        _toastLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _toastLabel.Position = new Vector2(0f, 620f);
        _toastLabel.Size = new Vector2(_root.Size.X, 32f);

        _loadingLabel = new Label
        {
            Text = "Preparing the pond...",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _loadingLabel.AddThemeFontSizeOverride("font_size", 22);
        _loadingLabel.AddThemeColorOverride("font_color", AccentColor);
        _loadingLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _loadingLabel.SetOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_loadingLabel);

        _confirmDialog = new ConfirmationDialog { Title = "Confirm", Size = new Vector2I(420, 180) };
        AddChild(_confirmDialog);

        _menuPanel.Visible = false;
        _newGamePanel.Visible = false;
        _loadPanel.Visible = false;
        _savePanel.Visible = false;
        _pausePanel.Visible = false;
        _extinctPanel.Visible = false;
        _diversityPanel.Visible = false;
        _advancedPanel.Visible = false;
        _fastForwardPanel.Visible = false;
        _fastForwardOverlay.Visible = false;
        _hud.Visible = false;
        _toastLabel.Visible = false;
        _hintLabel.Visible = false;
        _inspector.Visible = false;
        _shade.Visible = false;
    }

    private void BuildHud()
    {
        _hud = new PanelContainer();
        _hud.AddThemeStyleboxOverride("panel", PanelStyle(PanelColor, 0));
        _root.AddChild(_hud);
        _hud.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _hud.OffsetLeft = 0f;
        _hud.OffsetRight = 0f;
        _hud.OffsetTop = 0f;
        _hud.OffsetBottom = 64f;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _hud.AddChild(margin);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        _hudLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _hudLabel.AddThemeFontSizeOverride("font_size", 15);
        row.AddChild(_hudLabel);

        _pauseButton = MakeButton("Pause", 76);
        _pauseButton.Pressed += ToggleHudPause;
        row.AddChild(_pauseButton);
        foreach (var speed in new[] { 1, 2, 4, 8 })
        {
            var value = speed;
            var button = MakeButton($"{speed}x", 48);
            button.Pressed += () => SetSpeed(value);
            row.AddChild(button);
        }

        _feedButton = MakeButton("Feed", 120);
        _feedButton.Pressed += Feed;
        row.AddChild(_feedButton);
        var diversity = MakeButton("Diversity", 110);
        diversity.Pressed += OpenDiversity;
        row.AddChild(diversity);
        var iterate = MakeButton("Iterate", 92);
        iterate.Pressed += OpenFastForward;
        row.AddChild(iterate);
        var save = MakeButton("Save", 74);
        save.Pressed += OpenSave;
        row.AddChild(save);
        var menu = MakeButton("Menu", 74);
        menu.Pressed += OpenPause;
        row.AddChild(menu);

        _hintLabel = new Label
        {
            Text = "Wheel zoom · right-drag pan · R reset view · click a creature to inspect · U unlocks.",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _hintLabel.AddThemeFontSizeOverride("font_size", 13);
        _hintLabel.AddThemeColorOverride("font_color", MutedColor);
        _hintLabel.AddThemeConstantOverride("outline_size", 4);
        _hintLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        _root.AddChild(_hintLabel);
        _hintLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _hintLabel.OffsetLeft = 0f;
        _hintLabel.OffsetRight = 0f;
        _hintLabel.OffsetTop = -38f;
        _hintLabel.OffsetBottom = -12f;
    }

    private void BuildInspector()
    {
        _inspector = new PanelContainer();
        _inspector.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.035f, 0.08f, 0.11f, 0.97f), 10));
        _root.AddChild(_inspector);
        _inspector.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _inspector.OffsetLeft = -470f;
        _inspector.OffsetTop = -360f;
        _inspector.OffsetRight = -20f;
        _inspector.OffsetBottom = -58f;

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right" })
            margin.AddThemeConstantOverride(side, 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _inspector.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);

        _inspectorTitle = new Label { Text = "Creature" };
        _inspectorTitle.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(_inspectorTitle);
        var lookRow = new HBoxContainer();
        lookRow.AddThemeConstantOverride("separation", 10);
        _inspectorSwatch = new ColorRect
        {
            CustomMinimumSize = new Vector2(26f, 26f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        lookRow.AddChild(_inspectorSwatch);
        _inspectorLook = new Label { Modulate = MutedColor };
        _inspectorLook.AddThemeFontSizeOverride("font_size", 13);
        lookRow.AddChild(_inspectorLook);
        column.AddChild(lookRow);
        _inspectorStats = new Label();
        _inspectorStats.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_inspectorStats);
        _inspectorGenome = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            CustomMinimumSize = new Vector2(420f, 78f),
        };
        _inspectorGenome.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_inspectorGenome);
        _inspectorSensors = new Label { Modulate = MutedColor };
        _inspectorSensors.AddThemeFontSizeOverride("font_size", 12);
        column.AddChild(_inspectorSensors);
    }

    private void BuildDiversityPanel()
    {
        _diversityPanel = CenterPanel(820f, 620f);
        var column = VBox(_diversityPanel);
        column.AddChild(Title("DIVERSITY BOARD", 28));
        _diversityIndex = new Label();
        _diversityIndex.AddThemeFontSizeOverride("font_size", 30);
        _diversityIndex.AddThemeColorOverride("font_color", AccentColor);
        column.AddChild(_diversityIndex);
        _diversityStats = new Label { Modulate = MutedColor };
        _diversityStats.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_diversityStats);
        column.AddChild(LabelText("Diversity over the last samples", MutedColor));
        _diversityChart = new DiversitySparkline
        {
            CustomMinimumSize = new Vector2(0f, 130f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        column.AddChild(_diversityChart);
        var split = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 24);
        var themeColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        themeColumn.AddChild(LabelText("COLOR THEMES", MutedColor));
        _diversityThemeRows = new VBoxContainer();
        _diversityThemeRows.AddThemeConstantOverride("separation", 3);
        themeColumn.AddChild(_diversityThemeRows);
        split.AddChild(themeColumn);
        var lineageColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        lineageColumn.AddChild(LabelText("LINEAGES", MutedColor));
        _diversityLineageRows = new VBoxContainer();
        _diversityLineageRows.AddThemeConstantOverride("separation", 3);
        lineageColumn.AddChild(_diversityLineageRows);
        split.AddChild(lineageColumn);
        column.AddChild(split);
        _diversityPatterns = new Label { Modulate = MutedColor };
        _diversityPatterns.AddThemeFontSizeOverride("font_size", 12);
        column.AddChild(_diversityPatterns);
        var close = WideButton("Close");
        close.Pressed += CloseModals;
        column.AddChild(close);
    }

    private void OpenDiversity()
    {
        if (_busy || !_gameVisible)
            return;
        HideAllPanels();
        _diversityPanel.Visible = true;
        _shade.Visible = true;
        _toastLabel.Visible = false;
        _diversityTimer = 0d;
        RefreshDiversity();
    }

    private void UpdateDiversity(double delta)
    {
        if (!_gameVisible || !_diversityPanel.Visible)
            return;
        _diversityTimer -= delta;
        if (_diversityTimer > 0d)
            return;
        _diversityTimer = 0.5d;
        RefreshDiversity();
    }

    private void RefreshDiversity()
    {
        var session = _entry.Runtime.SessionManager.ForegroundSession;
        if (session is null)
            return;

        var genomes = new List<string>();
        var bodySet = new HashSet<string>(StringComparer.Ordinal);
        var themes = new int[CreatureAppearance.ThemeCount];
        var themeColors = new Color[CreatureAppearance.ThemeCount];
        var patterns = new int[CreatureAppearance.PatternCount];
        var lineages = new Dictionary<int, int>();
        foreach (var entity in session.GetEntities())
        {
            var (found, kind) = entity.TryGetData<string>(DemoKeys.Kind);
            if (!found || !string.Equals(kind, DemoKeys.CreatureKind, StringComparison.Ordinal))
                continue;
            var genome = ReadString(entity, DemoKeys.BodyGenome, string.Empty);
            if (genome.Length == 0)
                continue;
            genomes.Add(genome);
            bodySet.Add(genome);
            var look = GenomeAppearance.Derive(genome);
            if (themes[look.ThemeIndex] == 0)
                themeColors[look.ThemeIndex] = ToColor(look.Body);
            themes[look.ThemeIndex]++;
            patterns[(int)look.Pattern]++;
            var lineage = ReadInt(entity, DemoKeys.Lineage, 1);
            lineages[lineage] = lineages.GetValueOrDefault(lineage) + 1;
        }

        var metrics = GenomeDiversity.Analyze(genomes);
        var index = Math.Clamp(
            (metrics.MeanPairwiseDistance * 0.65f) + (metrics.MeanAlleleEntropy * 0.35f),
            0f, 1f);
        _diversityStats.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Population {metrics.Population}   Genomes {bodySet.Count}   Lineages {lineages.Count}\n" +
            $"Mean Hamming {metrics.MeanPairwiseDistance:0.000}   Allele entropy {metrics.MeanAlleleEntropy:0.000}");
        _diversityIndex.Text = string.Create(CultureInfo.InvariantCulture, $"Diversity index  {index:0.00}");
        _diversityChart.Push(index);

        var themeRows = new List<(string Name, int Count, Color Color)>(CreatureAppearance.ThemeCount);
        for (var i = 0; i < themes.Length; i++)
            if (themes[i] > 0)
                themeRows.Add((GenomeAppearance.ThemeNameFor(i), themes[i], themeColors[i]));
        FillDistribution(_diversityThemeRows, themeRows);

        var lineageRows = lineages
            .OrderByDescending(pair => pair.Value)
            .Take(8)
            .Select(pair => (
                Name: $"lineage {pair.Key}",
                Count: pair.Value,
                Color: Color.FromHsv(Mathf.PosMod(pair.Key * 0.618034f, 1f), 0.62f, 0.98f)))
            .ToList();
        FillDistribution(_diversityLineageRows, lineageRows);

        var patternSummary = string.Join(" · ", patterns
            .Select((count, i) => (count, i))
            .Where(item => item.count > 0)
            .OrderByDescending(item => item.count)
            .Take(6)
            .Select(item => $"{(CreaturePattern)item.i} {item.count}"));
        _diversityPatterns.Text = $"Patterns: {patternSummary}";
    }

    private static void FillDistribution(
        VBoxContainer host,
        List<(string Name, int Count, Color Color)> rows)
    {
        foreach (var child in host.GetChildren())
        {
            host.RemoveChild(child);
            child.QueueFree();
        }

        if (rows.Count == 0)
        {
            host.AddChild(LabelText("—", MutedColor));
            return;
        }

        var max = Math.Max(1, rows[0].Count);
        foreach (var row in rows)
        {
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 6);
            line.AddChild(new ColorRect
            {
                Color = row.Color,
                CustomMinimumSize = new Vector2(10f, 10f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            var label = new Label
            {
                Text = row.Name,
                CustomMinimumSize = new Vector2(120f, 0f),
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            line.AddChild(label);
            var bar = new ProgressBar
            {
                MaxValue = max,
                Value = row.Count,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(120f, 12f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            bar.AddThemeStyleboxOverride("background", PanelStyle(new Color(0.08f, 0.14f, 0.18f, 0.8f), 4));
            bar.AddThemeStyleboxOverride("fill", PanelStyle(row.Color, 4));
            line.AddChild(bar);
            var value = new Label { Text = row.Count.ToString(CultureInfo.InvariantCulture) };
            value.AddThemeFontSizeOverride("font_size", 12);
            line.AddChild(value);
            host.AddChild(line);
        }
    }

    private static Color ToColor(AppearanceColor color) => new(color.R, color.G, color.B);

    private void OpenAdvancedSettings()
    {
        if (_busy)
            return;
        HideAllPanels();
        _advancedPanel.Visible = true;
        _shade.Visible = true;
        _hud.Visible = false;
        _hintLabel.Visible = false;
        foreach (var row in _advancedNumbers)
        {
            var value = row.Read(_settingsDraft);
            row.Slider.Value = value;
            row.Edit.Text = value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        foreach (var row in _advancedToggles)
            row.Toggle.ButtonPressed = row.Read(_settingsDraft);
    }

    private void BackToNewGameFromAdvanced()
    {
        HideAllPanels();
        _newGamePanel.Visible = true;
        _shade.Visible = true;
    }

    private void BuildAdvancedPanel()
    {
        _advancedPanel = CenterPanel(900f, 780f);
        var column = VBox(_advancedPanel);
        column.AddChild(Title("ADVANCED SETTINGS", 26));
        var note = LabelText("Changes apply when the next New Pond starts. Mutation, food preset, collision and bloom are in the New Pond page.", MutedColor);
        note.AutowrapMode = TextServer.AutowrapMode.Word;
        column.AddChild(note);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 540f),
        };
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(rows);
        column.AddChild(scroll);

        AddAdvancedSection(rows, "WORLD");
        AddNumberSetting(rows, "Map radius", 2_500f, 25_000f, 100f,
            s => s.PondRadius, (s, v) => s.PondRadius = v);
        AddNumberSetting(rows, "Initial population", 50f, 5_000f, 10f,
            s => s.InitialPopulation, (s, v) => s.InitialPopulation = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Population cap", 500f, 10_000f, 50f,
            s => s.PopulationCap, (s, v) => s.PopulationCap = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Initial food", 0f, 12_000f, 50f,
            s => s.InitialFood, (s, v) => s.InitialFood = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Food cap", 500f, 20_000f, 100f,
            s => s.FoodCap, (s, v) => s.FoodCap = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Food per second", 0f, 1_500f, 5f,
            s => s.FoodPerSecond, (s, v) => s.FoodPerSecond = v);
        AddNumberSetting(rows, "Fallback body radius", 1f, 20f, 0.5f,
            s => s.BodyRadius, (s, v) => s.BodyRadius = v);
        AddNumberSetting(rows, "Creature speed", 10f, 150f, 1f,
            s => s.CreatureSpeed, (s, v) => s.CreatureSpeed = v);
        AddNumberSetting(rows, "Turn speed", 0.5f, 8f, 0.1f,
            s => s.TurnSpeed, (s, v) => s.TurnSpeed = v);
        AddNumberSetting(rows, "Sense range", 40f, 500f, 5f,
            s => s.SenseRange, (s, v) => s.SenseRange = v);
        AddNumberSetting(rows, "Sense half-angle (rad)", 0.1f, 1.5f, 0.05f,
            s => s.SenseHalfAngle, (s, v) => s.SenseHalfAngle = v);
        AddNumberSetting(rows, "Simulation tick (s)", 0.05f, 0.5f, 0.01f,
            s => s.SimulationTickSeconds, (s, v) => s.SimulationTickSeconds = v);
        AddNumberSetting(rows, "Fallback pickup radius", 2f, 100f, 1f,
            s => s.FoodPickupRadius, (s, v) => s.FoodPickupRadius = v);
        AddNumberSetting(rows, "Initial energy", 10f, 300f, 5f,
            s => s.InitialEnergy, (s, v) => s.InitialEnergy = v);
        AddNumberSetting(rows, "Food energy", 1f, 100f, 1f,
            s => s.FoodEnergy, (s, v) => s.FoodEnergy = v);
        AddNumberSetting(rows, "Max age (s)", 30f, 1_200f, 10f,
            s => s.MaxAgeSeconds, (s, v) => s.MaxAgeSeconds = v);

        AddAdvancedSection(rows, "EVOLUTION / METABOLISM");
        AddNumberSetting(rows, "Base metabolism", 0.05f, 3f, 0.05f,
            s => s.BaseMetabolismPerSecond, (s, v) => s.BaseMetabolismPerSecond = v);
        AddNumberSetting(rows, "Move energy", 0f, 5f, 0.05f,
            s => s.MoveEnergyPerSecond, (s, v) => s.MoveEnergyPerSecond = v);
        AddNumberSetting(rows, "Turn energy", 0f, 2f, 0.05f,
            s => s.TurnEnergyPerSecond, (s, v) => s.TurnEnergyPerSecond = v);
        AddNumberSetting(rows, "Reproduction energy", 20f, 400f, 5f,
            s => s.ReproductionEnergy, (s, v) => s.ReproductionEnergy = v);
        AddNumberSetting(rows, "Parent cost", 0f, 300f, 5f,
            s => s.ReproductionParentCost, (s, v) => s.ReproductionParentCost = v);
        AddNumberSetting(rows, "Child energy", 0f, 200f, 5f,
            s => s.ReproductionChildEnergy, (s, v) => s.ReproductionChildEnergy = v);
        AddNumberSetting(rows, "Reproduction cooldown", 0.5f, 30f, 0.5f,
            s => s.ReproductionCooldownSeconds, (s, v) => s.ReproductionCooldownSeconds = v);
        AddNumberSetting(rows, "Founder mutation %", 0f, 50f, 1f,
            s => (float)(s.FounderMutationRate * 100d), (s, v) => s.FounderMutationRate = v / 100d);
        AddNumberSetting(rows, "Brain mutation %", 0f, 5f, 0.1f,
            s => (float)(s.BrainMutationRate * 100d), (s, v) => s.BrainMutationRate = v / 100d);
        AddNumberSetting(rows, "Body length mutation %", 0f, 10f, 0.1f,
            s => (float)(s.BodyLengthMutationRate * 100d), (s, v) => s.BodyLengthMutationRate = v / 100d);
        AddNumberSetting(rows, "Frameshift mutation %", 0f, 5f, 0.1f,
            s => (float)(s.FrameshiftMutationRate * 100d), (s, v) => s.FrameshiftMutationRate = v / 100d);
        AddNumberSetting(rows, "Lineage mutation threshold", 1f, 30f, 1f,
            s => s.LineageMutationThreshold, (s, v) => s.LineageMutationThreshold = Mathf.RoundToInt(v));

        AddAdvancedSection(rows, "BODY GENE RANGES");
        AddNumberSetting(rows, "Body radius base", 0.5f, 30f, 0.5f,
            s => s.BodyRadiusBase, (s, v) => s.BodyRadiusBase = v);
        AddNumberSetting(rows, "Food pickup padding", 0f, 100f, 1f,
            s => s.FoodPickupPadding, (s, v) => s.FoodPickupPadding = v);
        AddNumberSetting(rows, "Size min", 0.1f, 5f, 0.05f,
            s => s.BodySizeMin, (s, v) => s.BodySizeMin = v);
        AddNumberSetting(rows, "Size max", 0.1f, 5f, 0.05f,
            s => s.BodySizeMax, (s, v) => s.BodySizeMax = v);
        AddNumberSetting(rows, "Speed min", 1f, 500f, 1f,
            s => s.BodySpeedMin, (s, v) => s.BodySpeedMin = v);
        AddNumberSetting(rows, "Speed max", 1f, 500f, 1f,
            s => s.BodySpeedMax, (s, v) => s.BodySpeedMax = v);
        AddNumberSetting(rows, "Turn min", 0.05f, 20f, 0.05f,
            s => s.BodyTurnMin, (s, v) => s.BodyTurnMin = v);
        AddNumberSetting(rows, "Turn max", 0.05f, 20f, 0.05f,
            s => s.BodyTurnMax, (s, v) => s.BodyTurnMax = v);
        AddNumberSetting(rows, "Sense min", 5f, 2_000f, 5f,
            s => s.BodySenseMin, (s, v) => s.BodySenseMin = v);
        AddNumberSetting(rows, "Sense max", 5f, 2_000f, 5f,
            s => s.BodySenseMax, (s, v) => s.BodySenseMax = v);
        AddNumberSetting(rows, "Spike min", 1f, 2_000f, 1f,
            s => s.BodySpikeMin, (s, v) => s.BodySpikeMin = v);
        AddNumberSetting(rows, "Spike max", 1f, 2_000f, 1f,
            s => s.BodySpikeMax, (s, v) => s.BodySpikeMax = v);
        AddNumberSetting(rows, "Metabolism min", 0f, 100f, 0.05f,
            s => s.BodyMetabolismMin, (s, v) => s.BodyMetabolismMin = v);
        AddNumberSetting(rows, "Metabolism max", 0f, 100f, 0.05f,
            s => s.BodyMetabolismMax, (s, v) => s.BodyMetabolismMax = v);
        AddNumberSetting(rows, "Age min", 1f, 100_000f, 10f,
            s => s.BodyAgeMin, (s, v) => s.BodyAgeMin = v);
        AddNumberSetting(rows, "Age max", 1f, 100_000f, 10f,
            s => s.BodyAgeMax, (s, v) => s.BodyAgeMax = v);
        AddNumberSetting(rows, "Max energy base", 0f, 10_000f, 10f,
            s => s.BodyMaxEnergyBase, (s, v) => s.BodyMaxEnergyBase = v);
        AddNumberSetting(rows, "Max energy per size", 0f, 10_000f, 10f,
            s => s.BodyMaxEnergyPerSize, (s, v) => s.BodyMaxEnergyPerSize = v);

        AddAdvancedSection(rows, "SPIKE");
        AddNumberSetting(rows, "Spike threshold", 0.1f, 1f, 0.05f,
            s => s.SpikeThreshold, (s, v) => s.SpikeThreshold = v);
        AddNumberSetting(rows, "Spike energy cost", 0f, 200f, 1f,
            s => s.SpikeEnergyCost, (s, v) => s.SpikeEnergyCost = v);
        AddNumberSetting(rows, "Spike cost / pellet", 0f, 100f, 1f,
            s => s.SpikePerPelletCost, (s, v) => s.SpikePerPelletCost = v);
        AddNumberSetting(rows, "Spike cooldown / extra pellet", 0f, 10f, 0.05f,
            s => s.SpikeCooldownPerExtraPellet, (s, v) => s.SpikeCooldownPerExtraPellet = v);
        AddNumberSetting(rows, "Spike cost / unit", 0f, 10f, 0.01f,
            s => s.SpikeEnergyPerUnit, (s, v) => s.SpikeEnergyPerUnit = v);
        AddNumberSetting(rows, "Spike aim half-angle (deg)", 0f, 180f, 5f,
            s => s.SpikeAimHalfAngleRadians * 180f / MathF.PI,
            (s, v) => s.SpikeAimHalfAngleRadians = v * MathF.PI / 180f);
        AddNumberSetting(rows, "Spike beam radius", 0.1f, 500f, 0.5f,
            s => s.SpikeBeamRadius, (s, v) => s.SpikeBeamRadius = v);
        AddNumberSetting(rows, "Spike steal fraction", 0f, 1f, 0.05f,
            s => s.SpikeDrainFraction, (s, v) => s.SpikeDrainFraction = v);
        AddNumberSetting(rows, "Spike radius", 10f, 300f, 5f,
            s => s.SpikeRadius, (s, v) => s.SpikeRadius = v);
        AddNumberSetting(rows, "Spike cooldown", 0.5f, 30f, 0.5f,
            s => s.SpikeCooldownSeconds, (s, v) => s.SpikeCooldownSeconds = v);

        AddAdvancedSection(rows, "FOOD PATCHES / SOURCES");
        AddNumberSetting(rows, "Patch count", 0f, 64f, 1f,
            s => s.FoodPatchCount, (s, v) => s.FoodPatchCount = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Patch radius", 50f, 3_000f, 10f,
            s => s.FoodPatchRadius, (s, v) => s.FoodPatchRadius = v);
        AddNumberSetting(rows, "Patch falloff", 0f, 10f, 0.1f,
            s => s.FoodPatchFalloff, (s, v) => s.FoodPatchFalloff = v);
        AddNumberSetting(rows, "Local capacity", 10f, 2_000f, 10f,
            s => s.FoodPatchLocalCapacity, (s, v) => s.FoodPatchLocalCapacity = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Source scan interval", 0.1f, 10f, 0.1f,
            s => s.FoodSourceScanIntervalSeconds, (s, v) => s.FoodSourceScanIntervalSeconds = v);
        AddNumberSetting(rows, "Source spawn interval", 0.1f, 10f, 0.1f,
            s => s.FoodSourceSpawnIntervalSeconds, (s, v) => s.FoodSourceSpawnIntervalSeconds = v);
        AddNumberSetting(rows, "Source spawn burst", 1f, 50f, 1f,
            s => s.FoodSourceSpawnBurst, (s, v) => s.FoodSourceSpawnBurst = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Source walk interval", 0.1f, 5f, 0.1f,
            s => s.FoodSourceWalkIntervalSeconds, (s, v) => s.FoodSourceWalkIntervalSeconds = v);
        AddNumberSetting(rows, "Walk speed min", 0f, 100f, 1f,
            s => s.FoodSourceWalkSpeedMin, (s, v) => s.FoodSourceWalkSpeedMin = v);
        AddNumberSetting(rows, "Walk speed max", 0f, 100f, 1f,
            s => s.FoodSourceWalkSpeedMax, (s, v) => s.FoodSourceWalkSpeedMax = v);
        AddNumberSetting(rows, "Walk turn radians", 0f, 3.14f, 0.05f,
            s => s.FoodSourceWalkTurnRadians, (s, v) => s.FoodSourceWalkTurnRadians = v);
        AddNumberSetting(rows, "Cycle period min", 10f, 1_000f, 10f,
            s => s.FoodSourceCyclePeriodMin, (s, v) => s.FoodSourceCyclePeriodMin = v);
        AddNumberSetting(rows, "Cycle period max", 10f, 1_000f, 10f,
            s => s.FoodSourceCyclePeriodMax, (s, v) => s.FoodSourceCyclePeriodMax = v);
        AddNumberSetting(rows, "Cycle low min", 0f, 1f, 0.01f,
            s => s.FoodSourceCycleLowMin, (s, v) => s.FoodSourceCycleLowMin = v);
        AddNumberSetting(rows, "Cycle low max", 0f, 1f, 0.01f,
            s => s.FoodSourceCycleLowMax, (s, v) => s.FoodSourceCycleLowMax = v);
        AddNumberSetting(rows, "Cycle high min", 1f, 5f, 0.05f,
            s => s.FoodSourceCycleHighMin, (s, v) => s.FoodSourceCycleHighMin = v);
        AddNumberSetting(rows, "Cycle high max", 1f, 5f, 0.05f,
            s => s.FoodSourceCycleHighMax, (s, v) => s.FoodSourceCycleHighMax = v);

        AddAdvancedSection(rows, "BLOOM / FEEDING / MISC");
        AddNumberSetting(rows, "Bloom bonus", 0f, 2_000f, 50f,
            s => s.EarlyFoodBonus, (s, v) => s.EarlyFoodBonus = v);
        AddNumberSetting(rows, "Bloom ramp (s)", 10f, 600f, 10f,
            s => s.EarlyFoodRampSeconds, (s, v) => s.EarlyFoodRampSeconds = v);
        AddNumberSetting(rows, "Bloom spawn bonus", 0f, 500f, 5f,
            s => s.EarlyFoodSpawnBonus, (s, v) => s.EarlyFoodSpawnBonus = v);
        AddNumberSetting(rows, "Startup snacks", 0f, 10f, 1f,
            s => s.StartupFoodPerFounder, (s, v) => s.StartupFoodPerFounder = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Death food", 0f, 20f, 1f,
            s => s.DeathFoodCount, (s, v) => s.DeathFoodCount = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Carcass food interval", 0.1f, 60f, 0.05f,
            s => s.CarcassFoodIntervalSeconds, (s, v) => s.CarcassFoodIntervalSeconds = v);
        AddNumberSetting(rows, "Tough food share", 0f, 0.95f, 0.01f,
            s => s.ToughFoodShare, (s, v) => s.ToughFoodShare = v);
        AddNumberSetting(rows, "Toxic food share", 0f, 0.95f, 0.01f,
            s => s.ToxicFoodShare, (s, v) => s.ToxicFoodShare = v);
        AddNumberSetting(rows, "Tough food energy", 0f, 200f, 1f,
            s => s.ToughFoodEnergy, (s, v) => s.ToughFoodEnergy = v);
        AddNumberSetting(rows, "Toxic food energy", 0f, 200f, 1f,
            s => s.ToxicFoodEnergy, (s, v) => s.ToxicFoodEnergy = v);
        AddNumberSetting(rows, "Feed amount", 0f, 200f, 5f,
            s => s.FeedFoodCount, (s, v) => s.FeedFoodCount = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Feed cooldown", 0f, 30f, 0.5f,
            s => s.FeedCooldownSeconds, (s, v) => s.FeedCooldownSeconds = v);
        AddNumberSetting(rows, "AI update batch", 50f, 3_000f, 50f,
            s => s.AiUpdateBatch, (s, v) => s.AiUpdateBatch = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Max catch-up steps", 10f, 200f, 5f,
            s => s.MaxCatchUpStepsPerFrame, (s, v) => s.MaxCatchUpStepsPerFrame = v);
        AddNumberSetting(rows, "Oscillator period", 1f, 60f, 0.5f,
            s => s.OscillatorPeriodSeconds, (s, v) => s.OscillatorPeriodSeconds = v);
        AddNumberSetting(rows, "Drive threshold", 0f, 1f, 0.05f,
            s => s.DriveThreshold, (s, v) => s.DriveThreshold = v);
        AddNumberSetting(rows, "Collision iterations", 1f, 5f, 1f,
            s => s.CollisionIterations, (s, v) => s.CollisionIterations = Mathf.RoundToInt(v));

        AddAdvancedSection(rows, "GLOBAL OVERGROWTH");
        AddBooleanSetting(rows, "Overgrowth enabled",
            s => s.OvergrowthEnabled, (s, v) => s.OvergrowthEnabled = v);
        AddNumberSetting(rows, "Overgrowth scan interval", 1f, 120f, 1f,
            s => s.OvergrowthScanIntervalSeconds, (s, v) => s.OvergrowthScanIntervalSeconds = v);
        AddNumberSetting(rows, "Overgrowth cell size", 200f, 5_000f, 50f,
            s => s.OvergrowthCellSize, (s, v) => s.OvergrowthCellSize = v);
        AddNumberSetting(rows, "Overgrowth food threshold", 5f, 2_000f, 5f,
            s => s.OvergrowthFoodThreshold, (s, v) => s.OvergrowthFoodThreshold = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Overgrowth mutation %", 1f, 80f, 1f,
            s => (float)(s.OvergrowthMutationRate * 100d), (s, v) => s.OvergrowthMutationRate = v / 100d);
        AddNumberSetting(rows, "Overgrowth food cost", 1f, 500f, 1f,
            s => s.OvergrowthFoodCost, (s, v) => s.OvergrowthFoodCost = Mathf.RoundToInt(v));
        AddNumberSetting(rows, "Overgrowth child energy", 1f, 400f, 5f,
            s => s.OvergrowthChildEnergy, (s, v) => s.OvergrowthChildEnergy = v);

        var back = WideButton("Back to New Pond");
        back.Pressed += BackToNewGameFromAdvanced;
        column.AddChild(back);
    }

    private static void AddAdvancedSection(VBoxContainer rows, string title)
    {
        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", AccentColor);
        rows.AddChild(label);
    }

    private void AddBooleanSetting(
        VBoxContainer rows,
        string label,
        Func<DemoSettings, bool> read,
        Action<DemoSettings, bool> write)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = new Label { Text = label, CustomMinimumSize = new Vector2(230f, 0f) };
        name.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(name);
        var toggle = new CheckButton { ButtonPressed = read(_settingsDraft) };
        _advancedToggles.Add((toggle, read));
        toggle.Toggled += pressed => write(_settingsDraft, pressed);
        row.AddChild(toggle);
        rows.AddChild(row);
    }

    private void AddNumberSetting(
        VBoxContainer rows,
        string label,
        float min,
        float max,
        float step,
        Func<DemoSettings, float> read,
        Action<DemoSettings, float> write)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = new Label { Text = label, CustomMinimumSize = new Vector2(230f, 0f) };
        name.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(name);
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = read(_settingsDraft),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260f, 0f),
        };
        row.AddChild(slider);
        var edit = new LineEdit
        {
            Text = read(_settingsDraft).ToString("0.###", CultureInfo.InvariantCulture),
            CustomMinimumSize = new Vector2(90f, 0f),
        };
        row.AddChild(edit);
        _advancedNumbers.Add((slider, edit, read));
        slider.ValueChanged += value =>
        {
            var number = (float)value;
            write(_settingsDraft, number);
            edit.Text = number.ToString("0.###", CultureInfo.InvariantCulture);
        };
        void ApplyText(string text)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return;
            number = Mathf.Clamp(number, min, max);
            write(_settingsDraft, number);
            slider.Value = number;
            edit.Text = number.ToString("0.###", CultureInfo.InvariantCulture);
        }

        edit.TextSubmitted += ApplyText;
        edit.FocusExited += () => ApplyText(edit.Text);
        rows.AddChild(row);
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
        UpdateHud();
    }

    private void BuildMenu()
    {
        _menuPanel = CenterPanel(420f, 430f);
        var column = VBox(_menuPanel);
        column.AddChild(Title("ORIGO: PRIMORDIAL POOL", 30));
        var subtitle = new Label
        {
            Text = "watch small minds evolve\nfeed the pond · inspect bodies and brains · save a lineage",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = MutedColor,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 14);
        column.AddChild(subtitle);
        column.AddChild(Spacer(14));

        _continueButton = WideButton("Continue");
        _continueButton.Pressed += ContinueGame;
        column.AddChild(_continueButton);
        var newGame = WideButton("New Game");
        newGame.Pressed += OpenNewGame;
        column.AddChild(newGame);
        var load = WideButton("Load Game");
        load.Pressed += OpenLoad;
        column.AddChild(load);
        var quit = WideButton("Quit");
        quit.Pressed += () => GetTree().Quit();
        column.AddChild(quit);
    }

    private void BuildNewGame()
    {
        _newGamePanel = CenterPanel(460f, 620f);
        var column = VBox(_newGamePanel);
        column.AddChild(Title("NEW POND", 26));
        column.AddChild(LabelText("Seed (blank = random)", MutedColor));
        _seedInput = new LineEdit { PlaceholderText = "e.g. 12345" };
        column.AddChild(_seedInput);
        column.AddChild(LabelText("Mutation", MutedColor));
        _mutationOption = new OptionButton();
        _mutationOption.AddItem("Calm — 0.5% per gene");
        _mutationOption.AddItem("Drift — 1.5% per gene");
        _mutationOption.AddItem("Chaos — 3.0% per gene");
        _mutationOption.Selected = 1;
        column.AddChild(_mutationOption);
        column.AddChild(LabelText("Food", MutedColor));
        _foodOption = new OptionButton();
        _foodOption.AddItem("Sparse");
        _foodOption.AddItem("Normal");
        _foodOption.AddItem("Lush");
        _foodOption.Selected = 1;
        column.AddChild(_foodOption);
        column.AddChild(LabelText("Collision", MutedColor));
        _collisionOption = new OptionButton();
        _collisionOption.AddItem("Off — sparse");
        _collisionOption.AddItem("Soft separation");
        _collisionOption.Selected = 0;
        column.AddChild(_collisionOption);
        column.AddChild(LabelText("Early food bloom", MutedColor));
        _bloomOption = new OptionButton();
        _bloomOption.AddItem("Off");
        _bloomOption.AddItem("On — 3 min ramp");
        _bloomOption.Selected = 0;
        column.AddChild(_bloomOption);
        var advanced = WideButton("Advanced Settings…");
        advanced.Pressed += OpenAdvancedSettings;
        column.AddChild(advanced);
        column.AddChild(Spacer(8));
        var start = WideButton("Start");
        start.Pressed += NewGame;
        column.AddChild(start);
        var back = WideButton("Back");
        back.Pressed += BackToMenuOrPause;
        column.AddChild(back);
    }

    private void BuildLoadPanel()
    {
        _loadPanel = CenterPanel(720f, 520f);
        var column = VBox(_loadPanel);
        column.AddChild(Title("LOAD POND", 26));
        _loadStatus = LabelText(string.Empty, MutedColor);
        column.AddChild(_loadStatus);
        _loadList = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _loadList.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_loadList);
        column.AddChild(scroll);
        var back = WideButton("Back");
        back.Pressed += CloseModals;
        column.AddChild(back);
    }

    private void BuildSavePanel()
    {
        _savePanel = CenterPanel(760f, 540f);
        var column = VBox(_savePanel);
        column.AddChild(Title("SAVE POND", 26));
        _saveStatus = LabelText("Saving pauses the simulation.", MutedColor);
        column.AddChild(_saveStatus);
        _saveList = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _saveList.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_saveList);
        column.AddChild(scroll);
        var back = WideButton("Back");
        back.Pressed += CloseModals;
        column.AddChild(back);
    }

    private void BuildPausePanel()
    {
        _pausePanel = CenterPanel(380f, 390f);
        var column = VBox(_pausePanel);
        column.AddChild(Title("PAUSED", 26));
        var resume = WideButton("Resume");
        resume.Pressed += ResumeGame;
        column.AddChild(resume);
        var save = WideButton("Save Game");
        save.Pressed += OpenSave;
        column.AddChild(save);
        var load = WideButton("Load Game");
        load.Pressed += OpenLoad;
        column.AddChild(load);
        var newGame = WideButton("New Game");
        newGame.Pressed += OpenNewGame;
        column.AddChild(newGame);
        var mainMenu = WideButton("Main Menu");
        mainMenu.Pressed += ShowMainMenu;
        column.AddChild(mainMenu);
    }

    private void BuildExtinctPanel()
    {
        _extinctPanel = CenterPanel(420f, 300f);
        var column = VBox(_extinctPanel);
        column.AddChild(Title("EXTINCTION", 28));
        var label = LabelText(
            "The last creature has returned to the substrate.\nSeed a new pond, or load an earlier lineage.",
            MutedColor);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(label);
        var restart = WideButton("New Game");
        restart.Pressed += OpenNewGame;
        column.AddChild(restart);
        var load = WideButton("Load Game");
        load.Pressed += OpenLoad;
        column.AddChild(load);
        var menu = WideButton("Main Menu");
        menu.Pressed += ShowMainMenu;
        column.AddChild(menu);
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
