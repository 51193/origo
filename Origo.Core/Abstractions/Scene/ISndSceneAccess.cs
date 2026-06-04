using System.Collections.Generic;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     抽象 SND 场景访问能力，供 Core 层编排存读档流程。
///     仅负责数据转换（元数据列表的序列化/恢复），不触发策略生命周期钩子。
///     钩子触发由上层（SndRuntime / SessionRun）在调用前后统一编排。
/// </summary>
public interface ISndSceneAccess
{
    /// <summary>
    ///     收集当前场景中所有实体的元数据列表。
    ///     不触发 BeforeSave 钩子——钩子应由调用方在调用此方法前批量触发。
    /// </summary>
    IReadOnlyList<SndMetaData> BuildMetaList();

    /// <summary>
    ///     按元数据列表恢复所有实体（数据、节点、策略）。
    ///     不触发 AfterLoad 钩子——钩子应由调用方在调用此方法后批量触发。
    ///     不自动清除已有实体——调用方应在调用此方法前自行处理旧实体清理。
    /// </summary>
    void RecoverFromMetaList(IEnumerable<SndMetaData> metaList);
}
