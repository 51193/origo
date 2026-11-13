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
        PersistGameplayProgression();
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

}
