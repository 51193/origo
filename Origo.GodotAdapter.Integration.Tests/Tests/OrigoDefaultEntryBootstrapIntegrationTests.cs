using Origo.Core;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Serialization;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;

namespace Origo.GodotAdapter.Integration.Tests;

public class OrigoDefaultEntryBootstrapIntegrationTests
{
    [IntegrationTest(Description = "OrigoDefaultEntry default export property values are correct")]
    public void DefaultEntry_ExportProperties_AllDefaults()
    {
        var entry = new OrigoDefaultEntry();

        IntegrationTestRunner.AssertEqual("res://origo/entry/entry.json", entry.ConfigPath, "ConfigPath");
        IntegrationTestRunner.AssertEqual("res://origo/maps/scene_aliases.map", entry.SceneAliasMapPath, "SceneAliasMapPath");
        IntegrationTestRunner.AssertEqual("res://origo/maps/snd_templates.map", entry.SndTemplateMapPath, "SndTemplateMapPath");
        IntegrationTestRunner.AssertEqual("user://origo_saves", entry.SaveRootPath, "SaveRootPath");
        IntegrationTestRunner.AssertEqual("res://origo/initial", entry.InitialSaveRootPath, "InitialSaveRootPath");
        IntegrationTestRunner.Assert(entry.AutoDiscoverStrategies, "AutoDiscoverStrategies default should be true.");
    }
}
