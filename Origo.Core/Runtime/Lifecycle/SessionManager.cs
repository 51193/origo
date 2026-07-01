using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Logging;
using Origo.Core.Save;
using Origo.Core.Snd;
using Origo.Core.Snd.Scene;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     <see cref="ISessionManager" /> 的默认实现。
///     构造时接收 <see cref="ProgressRuntime" /> 与 <see cref="IBlackboard" />，
///     内部构建 <see cref="SessionManagerRuntime" /> 作为本层唯一运行时容器。
///     全权管理所有 <see cref="ISessionRun" /> 的生命周期：创建、持有、序列化/反序列化、销毁。
/// </summary>
internal sealed class SessionManager : ISessionManager
{
    private const string _logTag = nameof(SessionManager);
    private readonly ISndSceneHost _adapterSceneHost;
    private readonly SessionManagerRuntime _managerRuntime;
    private readonly Dictionary<string, MountedSession> _sessions = new(StringComparer.Ordinal);

    // NOTE: MountedSession stores SessionRun (concrete) rather than ISessionRun (interface)
    // because SessionManager is internal and always creates SessionRun instances.
    // This avoids repeated casts from ISessionRun to SessionRun for internal operations
    // (serialize, load, persist), while public-facing members still return ISessionRun.

    internal SessionManager(ProgressRuntime progressRuntime, IBlackboard progressBlackboard)
    {
        ArgumentNullException.ThrowIfNull(progressRuntime);
        ArgumentNullException.ThrowIfNull(progressBlackboard);
        _managerRuntime = new SessionManagerRuntime(progressRuntime, progressBlackboard);
        _adapterSceneHost = _managerRuntime.AdapterSceneHost;
    }

    /// <summary>获取所有参与 Process 帧更新的会话的键列表。</summary>
    internal IReadOnlyCollection<string> ProcessingKeys =>
        [.. _sessions.Where(kvp => kvp.Value.SyncProcess).Select(kvp => kvp.Key)];

    /// <inheritdoc />
    public bool CanCreateSessions => true;

    /// <inheritdoc />
    public ISessionRun? ForegroundSession =>
        _sessions.TryGetValue(ISessionManager.ForegroundKey, out var mounted) ? mounted.Session : null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Keys => [.. _sessions.Keys];

    /// <inheritdoc />
    public ISessionRun? TryGet(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        return TryGetMountedSession(key)?.Session;
    }

    /// <inheritdoc />
    public bool Contains(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        return TryGetMountedSession(key) is not null;
    }

    /// <inheritdoc />
    public ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false)
    {
        ValidateKey(key);
        ValidateLevelIdUnique(levelId, key);
        var session = CreateBackgroundSessionCore(levelId);
        session.GetSessionStateMachines().FlushAllAfterLoad();
        MountInternal(key, session, syncProcess);
        return session;
    }

    /// <inheritdoc />
    public void DestroySession(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (!_sessions.Remove(key, out var mounted))
            return;

        DisposeMountedSession(key, mounted);
    }

    /// <inheritdoc />
    public void ProcessAllSessions(double delta, bool includeForeground = false)
    {
        // Snapshot keys to allow modifications during iteration.
        var keys = _sessions.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!includeForeground && string.Equals(key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
                continue;
            if (!_sessions.TryGetValue(key, out var mounted) || !mounted.SyncProcess)
                continue;

            mounted.Session.SceneHost.ProcessAll(delta);
        }
    }

    /// <inheritdoc />
    public void KillPendingAllSessions()
    {
        var keys = _sessions.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!_sessions.TryGetValue(key, out var mounted))
                continue;

            mounted.Session.KillPending();
        }
    }

    // ── Internal methods for ProgressRun ──────────────────────────────

    /// <summary>
    ///     创建前台会话并自动挂载。若已有前台会话，先销毁旧的。
    /// </summary>
    internal ISessionRun CreateForegroundSession(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));

        ValidateLevelIdUnique(levelId, ISessionManager.ForegroundKey);

        var sessionParams = new SessionParameters(levelId, new Blackboard.Blackboard(), _adapterSceneHost, true);
        var session = new SessionRun(_managerRuntime, sessionParams, this);
        MountInternal(ISessionManager.ForegroundKey, session, true);
        return session;
    }

    /// <summary>
    ///     创建前台会话，从 payload 恢复状态，并自动挂载。若已有前台会话，先销毁旧的。
    /// </summary>
    internal ISessionRun CreateForegroundFromPayload(string levelId, LevelPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var session = CreateForegroundSession(levelId);
        _sessions[ISessionManager.ForegroundKey].Session.LoadFromPayload(payload);
        return session;
    }

    /// <summary>
    ///     销毁当前前台会话（若存在）。
    /// </summary>
    internal void DestroyForeground() => DestroySession(ISessionManager.ForegroundKey);

    /// <summary>
    ///     清空 adapter 场景宿主上的所有实体（用于首次挂载前台前，或前台会话不存在时的清理）。
    /// </summary>
    internal void ClearAdapterScene() => _adapterSceneHost.RemoveAllEntities();

    /// <summary>
    ///     将指定键的会话序列化为 <see cref="LevelPayload" />。
    /// </summary>
    internal LevelPayload SerializeSession(string key)
    {
        var session = RequireMountedSession(key).Session;
        return session.SerializeToPayload();
    }

    /// <summary>
    ///     将指定键的会话状态持久化到 current/ 目录。
    /// </summary>
    internal void PersistSession(string key)
    {
        var session = RequireMountedSession(key).Session;
        session.PersistLevelState();
    }

    /// <summary>
    ///     从 <see cref="LevelPayload" /> 恢复指定键的会话状态。
    /// </summary>
    internal void LoadSessionFromPayload(string key, LevelPayload payload)
    {
        var session = RequireMountedSession(key).Session;
        session.LoadFromPayload(payload);
    }

    /// <summary>
    ///     清除所有后台会话（Dispose 并移除）。前台会话不受影响。
    /// </summary>
    internal void ClearBackground()
    {
        var bgKeys = EnumerateManagedKeys(false);
        foreach (var key in bgKeys)
            DestroySession(key);
    }

    /// <summary>
    ///     清除所有会话（Dispose 并移除）。
    /// </summary>
    internal void Clear()
    {
        var keys = EnumerateManagedKeys(true);
        foreach (var key in keys)
            DestroySession(key);
    }

    /// <summary>
    ///     获取指定键的会话是否参与 Process 帧更新。
    ///     若键不存在则返回 false。
    /// </summary>
    internal bool GetSyncProcess(string key) => !string.IsNullOrWhiteSpace(key) &&
                                                _sessions.TryGetValue(key, out var mounted) && mounted.SyncProcess;

    /// <summary>
    ///     获取所有已挂载的后台会话（不含前台）。
    /// </summary>
    internal IReadOnlyList<KeyValuePair<string, ISessionRun>> GetBackgroundSessions()
    {
        return [.. _sessions
            .Where(kvp => !string.Equals(kvp.Key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
            .Select(kvp => new KeyValuePair<string, ISessionRun>(kvp.Key, kvp.Value.Session))];
    }

    /// <summary>
    ///     将所有后台会话序列化为 LevelPayload 字典（key → LevelPayload）。
    /// </summary>
    internal IReadOnlyDictionary<string, LevelPayload> SerializeBackgroundSessions()
    {
        var result = new Dictionary<string, LevelPayload>();
        foreach (var kvp in _sessions)
        {
            if (string.Equals(kvp.Key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
                continue;
            result[kvp.Key] = kvp.Value.Session.SerializeToPayload();
        }

        return result;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private SessionRun CreateBackgroundSessionCore(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));

        var sceneHost = new FullMemorySndSceneHost(_managerRuntime.Logger);
        sceneHost.BindWorld(_managerRuntime.SndWorld);
        sceneHost.BindContext(_managerRuntime.SndContext);

        var sessionParams = new SessionParameters(levelId, new Blackboard.Blackboard(), sceneHost);
        return new SessionRun(_managerRuntime, sessionParams, this);
    }

    private void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Session key cannot be null or whitespace.", nameof(key));
        if (TryGetMountedSession(key) is not null)
            throw new InvalidOperationException($"A session with key '{key}' is already mounted.");
    }

    private void ValidateLevelIdUnique(string levelId, string newSessionKey)
    {
        foreach (var (existingKey, mounted) in _sessions)
            if (string.Equals(mounted.Session.LevelId, levelId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Cannot create session '{newSessionKey}' with levelId '{levelId}': " +
                    $"session '{existingKey}' already manages this level. " +
                    "Destroy the existing session before reusing its levelId.");
    }

    private void MountInternal(string key, SessionRun session, bool syncProcess)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Session key cannot be null or whitespace.", nameof(key));

        var watch = Stopwatch.StartNew();

        // If mounting foreground and old foreground exists, destroy old first.
        if (string.Equals(key, ISessionManager.ForegroundKey, StringComparison.Ordinal)
            && TryGetMountedSession(key) is not null)
            DestroyForeground();

        if (TryGetMountedSession(key) is not null)
            throw new InvalidOperationException($"A session with key '{key}' is already mounted.");

        _sessions[key] = new MountedSession(session, syncProcess);
        _managerRuntime.Logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Mounted session '{key}' (level: {session.LevelId}, syncProcess: {syncProcess})."));

        session.MountKey = key;
        session.Disposing += () =>
        {
            if (session.MountKey is not null)
                _sessions.Remove(session.MountKey);
        };
    }

    private MountedSession? TryGetMountedSession(string key) =>
        _sessions.TryGetValue(key, out var mounted) ? mounted : null;

    private MountedSession RequireMountedSession(string key)
    {
        return TryGetMountedSession(key) ??
               throw new InvalidOperationException($"No session with key '{key}' is mounted.");
    }

    private string[] EnumerateManagedKeys(bool includeForeground)
    {
        return [.. _sessions.Keys.Where(key =>
                includeForeground || !string.Equals(key, ISessionManager.ForegroundKey, StringComparison.Ordinal))];
    }

    private void DisposeMountedSession(string key, MountedSession mounted)
    {
        var watch = Stopwatch.StartNew();
        _managerRuntime.Logger.Log(LogLevel.Info, _logTag,
            $"Destroying session '{key}' (level: {mounted.Session.LevelId}).");

        mounted.Session.MountKey = null;
        mounted.Session.Dispose();
        _managerRuntime.Logger.Log(LogLevel.Info, _logTag,
            new LogMessageBuilder()
                .SetElapsedMs(watch.Elapsed.TotalMilliseconds)
                .Build($"Destroyed session '{key}'."));
    }

    private sealed record MountedSession(SessionRun Session, bool SyncProcess);
}
