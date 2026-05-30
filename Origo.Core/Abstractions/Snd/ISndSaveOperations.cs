using System.Collections.Generic;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供存档读写、关卡切换与实体清空等持久化相关操作。
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
    ///     请求清理当前场景中所有实体（帧末延迟执行，
    ///     与 RequestSaveGame / RequestSwitchForegroundLevel 等对齐）。
    /// </summary>
    void RequestClearEntities();

    /// <summary>
    ///     请求按名称销毁单个实体（帧末延迟执行）。
    ///     若实体不存在则静默忽略。
    /// </summary>
    void RequestKillEntity(string entityName);
}