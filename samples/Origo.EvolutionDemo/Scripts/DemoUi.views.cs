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

public partial class DemoUi
{
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

}
