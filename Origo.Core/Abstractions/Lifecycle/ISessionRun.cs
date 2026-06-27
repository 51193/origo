using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Abstractions.Lifecycle;

/// <summary>
///     关卡会话级运行时的门面接口。
///     策略经此访问会话能力：实体操作（查找/生成/kill）、黑板、状态机、
///     以及所属的 <see cref="ISessionManager" />（跨会话入口）。
///     生命周期（创建 / 销毁）与序列化均由 <see cref="ISessionManager" /> 管理。
///     前台和后台会话为同一接口，差异仅在于 <see cref="IsFrontSession" />。
/// </summary>
public interface ISessionRun : IDisposable
{
    IBlackboard SessionBlackboard { get; }

    string LevelId { get; }

    bool IsFrontSession { get; }

    IStateMachineContainer GetSessionStateMachines();

    /// <summary>
    ///     该会话所属的 <see cref="ISessionManager" />。
    ///     策略经此跨会话访问其它会话。
    /// </summary>
    ISessionManager SessionManager { get; }

    // ── 实体操作（会话作用域） ──

    ISndEntity? FindByName(string name);
    IReadOnlyCollection<ISndEntity> GetEntities();
    ISndEntity Spawn(SndMetaData meta);
    void SpawnMany(params SndMetaData[] metaList);
    void RequestKillEntity(string entityName);
}
