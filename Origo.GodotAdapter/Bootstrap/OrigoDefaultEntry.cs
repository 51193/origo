using Godot;
using Origo.Core.DataSource;
using Origo.Core.Snd;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     Default program entry node. Inherits <see cref="OrigoAutoHost" /> to gain runtime self-binding capability,
///     and delegates automatic initialization orchestration to the Core layer's <c>ISndContext.Bootstrap</c>.
///     The Adapter layer only provides Godot-specific I/O implementations and engine assembly filtering prefixes,
///     containing no orchestration logic.
/// </summary>
[GlobalClass]
public partial class OrigoDefaultEntry : OrigoAutoHost
{
    private static readonly string[] _godotSkipPrefixes = ["Godot", "GodotSharp"];

    /// <summary>Path to the entry config file (levels-structured <c>entry.json</c>).</summary>
    [Export] public string ConfigPath { get; set; } = "res://origo/entry/entry.json";

    /// <summary>Scene alias mapping file path; loaded during <see cref="SndContext.Bootstrap" />.</summary>
    [Export] public string SceneAliasMapPath { get; set; } = "res://origo/maps/scene_aliases.map";

    /// <summary>SND template mapping file path; loaded during <see cref="SndContext.Bootstrap" />.</summary>
    [Export] public string SndTemplateMapPath { get; set; } = "res://origo/maps/snd_templates.map";

    /// <summary>Root directory for runtime saves.</summary>
    [Export] public string SaveRootPath { get; set; } = "user://origo_saves";

    /// <summary>Root directory for the initial (res://) saves.</summary>
    [Export] public string InitialSaveRootPath { get; set; } = "res://origo/initial";

    /// <summary>Whether to auto-discover strategy types during <see cref="SndContext.Bootstrap" />.</summary>
    [Export] public bool AutoDiscoverStrategies { get; set; } = true;

    /// <summary>
    ///     Called after <see cref="SndContext" /> is created and bound to <see cref="GodotSndManager" />;
    ///     subclasses can override and register display <c>meta.map</c> contributors via
    ///     <c>context.Save.RegisterSaveMetaContributor(ISaveMetaContributor)</c> or
    ///     <c>context.Save.RegisterSaveMetaContributor(Func&lt;SaveMetaBuildContext, IReadOnlyDictionary&lt;string, string&gt;&gt;)</c>.
    /// </summary>
    protected virtual void ConfigureSaveMetadataContributors(ISndContext context)
    {
    }

    /// <summary>
    ///     Custom type converter registration hook. Called before <see cref="SndContext.Bootstrap" />.
    ///     Override this method to register custom <see cref="DataSourceConverter{T}" />,
    ///     ensuring custom types are available before strategy auto-discovery, template loading,
    ///     and entry save loading.
    /// </summary>
    protected virtual void RegisterCustomConverters(DataSourceConverterRegistry registry)
    {
    }
}
