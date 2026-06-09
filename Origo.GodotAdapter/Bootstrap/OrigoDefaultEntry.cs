using Godot;
using Origo.Core.DataSource;
using Origo.Core.Snd;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     默认程序入口节点。继承 <see cref="OrigoAutoHost" /> 获得运行时自绑定能力，
///     并将自动初始化编排委托给 Core 层的 <see cref="OrigoAutoInitializer" />。
///     Adapter 层仅提供 Godot 特有的 I/O 实现与引擎程序集过滤前缀，不包含任何编排逻辑。
/// </summary>
[GlobalClass]
public partial class OrigoDefaultEntry : OrigoAutoHost
{
    private static readonly string[] GodotSkipPrefixes = ["Godot", "GodotSharp"];

    private SndContext? _sndContext;

    [Export] public string ConfigPath { get; set; } = "res://origo/entry/entry.json";
    [Export] public string SceneAliasMapPath { get; set; } = "res://origo/maps/scene_aliases.map";
    [Export] public string SndTemplateMapPath { get; set; } = "res://origo/maps/snd_templates.map";
    [Export] public string SaveRootPath { get; set; } = "user://origo_saves";
    [Export] public string InitialSaveRootPath { get; set; } = "res://origo/initial";
    [Export] public bool AutoDiscoverStrategies { get; set; } = true;

    /// <summary>
    ///     在 <see cref="SndContext" /> 创建并绑定到 <see cref="GodotSndManager" /> 之后调用；
    ///     子类可覆写并在其中通过
    ///     <c>context.RegisterSaveMetaContributor(ISaveMetaContributor)</c> 或
    ///     <c>context.RegisterSaveMetaContributor(Action&lt;...&gt;)</c>
    ///     注册展示用 <c>meta.map</c> 贡献者。
    /// </summary>
    protected virtual void ConfigureSaveMetadataContributors(ISndContext context)
    {
    }

    /// <summary>
    ///     自定义类型转换器注册钩子。在 <see cref="SndContext.Bootstrap" /> 之前调用。
    ///     Override 此方法以注册自定义 <see cref="DataSourceConverter{T}" />，
    ///     确保自定义类型在策略自动发现、模板加载和入口存档加载之前可用。
    /// </summary>
    protected virtual void RegisterCustomConverters(DataSourceConverterRegistry registry)
    {
    }
}
