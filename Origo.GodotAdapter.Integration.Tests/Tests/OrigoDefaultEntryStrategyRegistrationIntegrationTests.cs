using System;
using Godot;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

/// <summary>
///     The default entry exposes a pre-Bootstrap strategy registration hook so
///     derived entries can register strategies that auto-discovery cannot cover.
/// </summary>
public partial class OrigoDefaultEntryStrategyRegistrationIntegrationTests : IDeferredTestFixture, IDisposable
{
    private const string _probeIndex = "integration.configure_strategies_probe";

    private StrategyRegisteringEntry? _entry;
    private int _frame;

    public bool IsComplete => _frame >= 1;

    public void Setup()
    {
        _frame = 0;
        _entry = new StrategyRegisteringEntry { Name = "StrategyRegisteringEntry" };
    }

    public void AdvanceFrame() => _frame++;

    [DeferredTest(Description = "OrigoDefaultEntry.ConfigureStrategies registers strategies before Bootstrap seals the registry")]
    public void DefaultEntry_Ready_RegistersConfiguredStrategyBeforeBootstrap()
    {
        var entry = _entry!;
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(entry);

        IntegrationTestRunner.Assert(entry.HookCalled, "ConfigureStrategies must be called during _Ready");
        IntegrationTestRunner.Assert(
            entry.Runtime.SndWorld.IsStrategyRegistered(_probeIndex),
            "strategy registered in ConfigureStrategies must be present after Bootstrap");
    }

    public void Dispose()
    {
        if (_entry is not null)
        {
            IntegrationTestRunner.FreeNode(_entry);
            _entry = null;
        }

        GC.SuppressFinalize(this);
    }

    private sealed partial class StrategyRegisteringEntry : OrigoDefaultEntry
    {
        public bool HookCalled { get; private set; }

        public override void _Ready()
        {
            AutoDiscoverStrategies = false;
            ConfigPath = "res://TestScenes/test_entry_levels.json";
            SceneAliasMapPath = "res://TestScenes/empty_scene_aliases.map";
            SndTemplateMapPath = "res://TestScenes/empty_templates.map";
            InitialSaveRootPath = "res://TestScenes/empty_initial";
            SaveRootPath = "user://origo_configure_strategies_test_saves";
            base._Ready();
        }

        protected override void ConfigureStrategies(SndWorld world)
        {
            HookCalled = true;
            world.RegisterStrategy(() => new ProbeStrategy());
        }
    }

    [StrategyIndex(_probeIndex)]
    private sealed class ProbeStrategy : LifecycleStrategyBase
    {
    }
}
