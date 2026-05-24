using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.DataSource;
using Origo.Core.Save.Meta;

namespace Origo.Core.Save.Storage;

/// <summary>
///     存档读写服务抽象。封装存储布局与 I/O 操作为可替换接口，
///     使 SessionRun / ProgressRun / Workflow 等调用方与具体存储实现解耦，
///     可在不同运行模式（前台/后台/测试/云存档）间平滑替换。
///     所有路径拼装由实现内部的 <see cref="ISavePathPolicy" /> 决定，调用方无需感知布局。
/// </summary>
public interface ISaveStorageService
{
    /// <summary>枚举所有存档槽 ID。</summary>
    IReadOnlyList<string> EnumerateSaveIds();

    /// <summary>枚举所有存档槽及其元数据。</summary>
    IReadOnlyList<SaveMetaDataEntry> EnumerateSavesWithMetaData();

    /// <summary>将存档 payload 写入 current/ 目录。</summary>
    void WriteSavePayloadToCurrent(SaveGamePayload payload);

    /// <summary>将存档 payload 写入 current/ 后快照到 save_* 目录。</summary>
    void WriteSavePayloadToCurrentThenSnapshot(
        SaveGamePayload payload,
        string newSaveId,
        ILogger logger);

    /// <summary>仅写入单个关卡 payload 到指定基目录。</summary>
    void WriteLevelPayloadOnly(
        string baseDirectoryRel,
        LevelPayload levelPayload,
        bool overwrite = true);

    /// <summary>仅写入单个关卡 payload 到 current/ 目录。</summary>
    void WriteLevelPayloadOnlyToCurrent(LevelPayload levelPayload, bool overwrite = true);

    /// <summary>仅写入 Progress 相关文件到 current/ 目录。</summary>
    void WriteProgressOnlyToCurrent(
        DataSourceNode progressNode,
        DataSourceNode progressStateMachinesNode,
        bool overwrite = true);

    /// <summary>从 current/ 读取完整存档 payload。</summary>
    SaveGamePayload ReadSavePayloadFromCurrent(
        string saveId,
        string activeLevelId,
        ILogger? logger = null);

    /// <summary>从 save_* 快照目录读取完整存档 payload。</summary>
    SaveGamePayload ReadSavePayloadFromSnapshot(
        string saveId,
        string activeLevelId);

    /// <summary>从 save_* 快照目录仅读取 Progress 节点。</summary>
    DataSourceNode? ReadProgressNodeFromSnapshot(string saveId);

    /// <summary>从 current/ 中尝试读取指定关卡的 payload，不存在时返回 null。</summary>
    LevelPayload? TryReadLevelPayloadFromCurrent(string levelId);

    /// <summary>从 save_* 快照目录中尝试读取指定关卡的 payload，不存在时返回 null。</summary>
    LevelPayload? TryReadLevelPayloadFromSnapshot(string saveId, string levelId);

    /// <summary>
    ///     按优先级解析并读取指定关卡的 payload：优先从 current/ 目录读取，不存在时回退到 save_* 快照。
    ///     两处都不存在时返回 null。
    ///     此方法封装了存档模块的内部存储层级（current/ vs snapshot），外部调用方无需感知存储位置。
    /// </summary>
    /// <param name="saveId">当前存档槽 ID（用于快照回退时定位 save_* 目录）。</param>
    /// <param name="levelId">目标关卡 ID。</param>
    /// <returns>解析到的 LevelPayload，或 null（两处均无数据时）。</returns>
    LevelPayload? ResolveLevelPayload(string saveId, string levelId);

    /// <summary>将 current/ 快照到 save_* 目录。</summary>
    void SnapshotCurrentToSave(string newSaveId);

    /// <summary>
    ///     删除 current/ 临时活动目录及其全部内容。
    ///     设计意图：
    ///     - 在从快照读取并拷贝到 current/ 之前，先清理上一次的临时数据，避免旧文件残留；
    ///     - 在 ProgressRun 生命周期结束（退出当前流程）后，清理 current/ 以释放空间并避免误用。
    ///     实现应是幂等的：若目录不存在则不抛异常。
    /// </summary>
    void DeleteCurrentDirectory();
}
