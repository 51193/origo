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

}
