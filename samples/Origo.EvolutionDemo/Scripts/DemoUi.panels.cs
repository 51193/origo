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

}
