namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供实体级别的操作（标记销毁、批量清空等）。
///     与 <see cref="ISndSaveOperations" /> 分离，因为实体操作属于运行时生命周期，
///     不应混入存档职责。
/// </summary>
public interface ISndEntityOperations
{
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
