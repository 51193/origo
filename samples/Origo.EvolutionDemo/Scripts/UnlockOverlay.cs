using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>
///     Generic unlock shop driven entirely by unlock definitions from the
///     gameplay content registry. It is intentionally independent of the
///     concrete gene/mechanic list.
/// </summary>
public partial class UnlockOverlay : CanvasLayer
{
    private static readonly Color TextColor = new(0.9f, 0.95f, 0.97f);
    private static readonly Color MutedColor = new(0.58f, 0.68f, 0.72f);
    private static readonly Color AccentColor = new(0.36f, 0.9f, 0.76f);
    private static readonly Color PanelColor = new(0.035f, 0.07f, 0.1f, 0.98f);

    private readonly DemoEntry _entry;
    private Control _root = null!;
    private PanelContainer _panel = null!;
    private Button _openButton = null!;
    private Label _pointsLabel = null!;
    private VBoxContainer _rows = null!;
    private string _contentFingerprint = string.Empty;

    public UnlockOverlay(DemoEntry entry)
    {
        _entry = entry;
        Layer = 12;
    }

    public override void _Ready()
    {
        _root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _openButton = new Button
        {
            Text = "Unlocks (U)",
            CustomMinimumSize = new Vector2(130f, 36f),
            FocusMode = Control.FocusModeEnum.None,
        };
        _openButton.AddThemeColorOverride("font_color", TextColor);
        _openButton.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _openButton.Position = new Vector2(18f, -58f);
        _openButton.AnchorTop = 1f;
        _openButton.AnchorBottom = 1f;
        _openButton.OffsetLeft = 18f;
        _openButton.OffsetTop = -58f;
        _openButton.OffsetRight = 148f;
        _openButton.OffsetBottom = -18f;
        _openButton.Pressed += Open;
        _root.AddChild(_openButton);

        _panel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(620f, 480f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var style = new StyleBoxFlat
        {
            BgColor = PanelColor,
            ContentMarginLeft = 20f,
            ContentMarginRight = 20f,
            ContentMarginTop = 16f,
            ContentMarginBottom = 16f,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
        _panel.AddThemeStyleboxOverride("panel", style);
        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.Position = new Vector2(-310f, -240f);
        _root.AddChild(_panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(column);

        var title = new Label
        {
            Text = "EVOLUTION UNLOCKS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", AccentColor);
        column.AddChild(title);

        _pointsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _pointsLabel.AddThemeColorOverride("font_color", MutedColor);
        column.AddChild(_pointsLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 350f),
        };
        column.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_rows);

        var close = new Button { Text = "Close (Esc)" };
        close.Pressed += Close;
        column.AddChild(close);
    }

    public override void _Process(double delta)
    {
        if (_panel.Visible)
            Refresh();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        if (key.Keycode == Key.U)
        {
            if (_panel.Visible)
                Close();
            else
                Open();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Escape && _panel.Visible)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Open()
    {
        _panel.Visible = true;
        _openButton.Visible = false;
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        Refresh();
    }

    private void Close()
    {
        _panel.Visible = false;
        _openButton.Visible = true;
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private void Refresh()
    {
        var runtime = _entry.Session.Runtime;
        _pointsLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Evolution points: {runtime.Progression.Points:N0}");
        var fingerprint = string.Join('|',
            runtime.Content.Unlocks.Values
                .OrderBy(static unlock => unlock.Id, StringComparer.Ordinal)
                .Select(static unlock =>
                    $"{unlock.Id}:{unlock.Cost}:{unlock.Title}:{unlock.Category}:" +
                    $"{string.Join(',', unlock.Prerequisites)}:{string.Join(',', unlock.Grants)}:" +
                    $"{string.Join(',', unlock.RequiredMilestones)}"));
        if (string.Equals(fingerprint, _contentFingerprint, StringComparison.Ordinal)
            && _rows.GetChildCount() > 0)
        {
            return;
        }

        _contentFingerprint = fingerprint;
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var unlock in runtime.Content.Unlocks.Values
                     .OrderBy(static unlock => unlock.Category, StringComparer.Ordinal)
                     .ThenBy(static unlock => unlock.Title, StringComparer.Ordinal))
            _rows.AddChild(BuildRow(runtime, unlock));
    }

    private HBoxContainer BuildRow(GameplayRuntime runtime, UnlockDefinition unlock)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var text = new Label
        {
            Text = $"{unlock.Title}\n{unlock.Category} · {unlock.Cost:N0} EP" +
                   (unlock.Prerequisites.Count > 0
                       ? $" · requires {string.Join(", ", unlock.Prerequisites)}"
                       : string.Empty),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        text.AddThemeColorOverride("font_color", TextColor);
        row.AddChild(text);

        var unlocked = runtime.Unlocks.IsUnlocked(unlock.Id);
        var button = new Button
        {
            Text = unlocked ? "Unlocked" : "Unlock",
            Disabled = unlocked || !runtime.Unlocks.CanUnlock(unlock.Id),
            CustomMinimumSize = new Vector2(100f, 36f),
        };
        button.Pressed += () =>
        {
            if (!runtime.Unlocks.TryUnlock(unlock.Id))
            {
                _entry.Ui.ShowToast("Unlock requirements are no longer met.", true);
                return;
            }
            _entry.Ui.PersistGameplayProgression();
            _contentFingerprint = string.Empty;
            Refresh();
            _entry.Ui.ShowToast($"Unlocked {unlock.Title}");
        };
        row.AddChild(button);
        return row;
    }
}
