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
    ///     立即将当前场景中所有实体标记为待销毁。
    ///     实体的物理销毁在帧末统一执行（业务延迟队列之后、系统延迟队列之前）。
    ///     已标记为待销毁的实体会被跳过，不会重复标记。
    /// </summary>
    void RequestKillAll();

    /// <summary>
    ///     立即将指定实体标记为待销毁。
    ///     实体的物理销毁在帧末统一执行（业务延迟队列之后、系统延迟队列之前）。
    /// </summary>
    /// <exception cref="InvalidOperationException">若实体不存在或已标记为待销毁。</exception>
    void RequestKillEntity(string entityName);
}
