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

}
