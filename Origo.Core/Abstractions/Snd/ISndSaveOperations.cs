using System;
using System.Collections.Generic;
using Origo.Core.Save.Meta;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供存档读写与关卡切换等持久化相关操作。
/// </summary>
public interface ISndSaveOperations
{
    /// <summary>列出可用存档槽位。</summary>
    IReadOnlyList<string> ListSaves();

    /// <summary>请求加载指定存档。</summary>
    void RequestLoadGame(string saveId);

    /// <summary>请求保存到指定槽位。</summary>
    void RequestSaveGame(string newSaveId);

    /// <summary>自动保存，返回实际使用的 saveId。</summary>
    string RequestSaveGameAuto(string? newSaveId = null);

    /// <summary>设置 continue 目标存档。</summary>
    void SetContinueTarget(string saveId);

    /// <summary>请求切换前台关卡。</summary>
    void RequestSwitchForegroundLevel(string newLevelId);

    /// <summary>
    ///     注册展示用 <c>meta.map</c> 的贡献者，在每次 <see cref="RequestSaveGame" /> 时执行。
    /// </summary>
    void RegisterSaveMetaContributor(ISaveMetaContributor contributor);

    /// <summary>
    ///     通过委托注册展示用 <c>meta.map</c> 的贡献者。
    /// </summary>
    void RegisterSaveMetaContributor(Action<SaveMetaBuildContext, IDictionary<string, string>> contribute);
}
