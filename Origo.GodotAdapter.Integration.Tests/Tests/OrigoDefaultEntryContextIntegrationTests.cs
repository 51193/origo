using System;
using Godot;
using Origo.Core.Snd;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

/// <summary>
///     The default entry exposes the SND context to presentation-layer code
///     after _Ready, while still passing the same instance through the
///     protected metadata-contributor hook.
/// </summary>
public partial class OrigoDefaultEntryContextIntegrationTests : IDeferredTestFixture, IDisposable
{
    private ContextCapturingEntry? _entry;
    private int _frame;

    public bool IsComplete => _frame >= 1;

    public void Setup()
    {
        _frame = 0;
        _entry = new ContextCapturingEntry { Name = "ContextCapturingEntry" };
    }

    public void AdvanceFrame() => _frame++;

    [DeferredTest(Description = "OrigoDefaultEntry exposes the created SndContext after successful bootstrap setup")]
    public void DefaultEntry_Ready_ExposesContextAndSharesItWithConfigureHook()
    {
        var entry = _entry!;
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(entry);

        IntegrationTestRunner.AssertNotNull(entry.Context, "Context");
        IntegrationTestRunner.Assert(
            ReferenceEquals(entry.Context, entry.CapturedContext),
            "Context must be the same instance passed to ConfigureSaveMetadataContributors");
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

    private sealed partial class ContextCapturingEntry : OrigoDefaultEntry
    {
        public ISndContext? CapturedContext { get; private set; }

        public override void _Ready()
        {
            ConfigPath = "res://TestScenes/test_entry_levels.json";
            SceneAliasMapPath = "res://TestScenes/empty_scene_aliases.map";
            SndTemplateMapPath = "res://TestScenes/empty_templates.map";
            InitialSaveRootPath = "res://TestScenes/empty_initial";
            SaveRootPath = "user://origo_context_test_saves";
            base._Ready();
        }

        protected override void ConfigureSaveMetadataContributors(ISndContext context)
        {
            CapturedContext = context;
            base.ConfigureSaveMetadataContributors(context);
        }
    }
}
