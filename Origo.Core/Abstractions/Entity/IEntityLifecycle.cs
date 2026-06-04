using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Entity;

/// <summary>
///     实体生命周期管理接口。
///     供框架内部编排实体创建/加载/保存/销毁的两阶段批处理流程。
///     此接口为 internal，业务代码不可访问。
///     <para>
///         实现者：<see cref="Origo.Core.Snd.Entity.SndEntity" />（Core 内存实体）、
///         GodotSndEntity（Godot 适配层实体，委托给内部 SndEntity）。
///     </para>
/// </summary>
internal interface IEntityLifecycle
{
    /// <summary>
    ///     Phase 1：从元数据恢复实体的数据、节点和所有策略（EntityStrategy + ActiveStrategy），不触发任何钩子。
    /// </summary>
    void RecoverForLifecycle(SndMetaData metaData);

    /// <summary>
    ///     Phase 2：触发实体策略的 AfterSpawn 钩子。
    /// </summary>
    void FireAfterSpawnHooks();

    /// <summary>
    ///     Phase 2：触发实体策略的 AfterLoad 钩子。
    /// </summary>
    void FireAfterLoadHooks();

    /// <summary>
    ///     Phase 2：触发实体策略的 BeforeSave 钩子。
    /// </summary>
    void FireBeforeSaveHooks();

    /// <summary>
    ///     Phase 2：触发实体策略的 BeforeQuit 钩子。
    /// </summary>
    void FireBeforeQuitHooks();

    /// <summary>
    ///     Phase 2：触发实体策略的 BeforeDead 钩子。
    /// </summary>
    void FireBeforeDeadHooks();

    /// <summary>
    ///     Phase 3：仅释放实体策略和主动策略的引用，不触发钩子。
    /// </summary>
    void ReleaseStrategiesOnly();

    /// <summary>
    ///     Phase 3：仅释放节点和数据资源，不触发钩子。
    /// </summary>
    void TeardownOnly();

    /// <summary>
    ///     构建实体元数据，不触发 BeforeSave 钩子。
    /// </summary>
    SndMetaData BuildMetaData();
}
