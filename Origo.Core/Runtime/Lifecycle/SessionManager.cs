using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Save;
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
    private const string LogTag = "SessionManager";
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
    }

    /// <summary>获取所有参与 Process 帧更新的会话的键列表。</summary>
    internal IReadOnlyCollection<string> ProcessingKeys =>
        _sessions.Where(kvp => kvp.Value.SyncProcess)
            .Select(kvp => kvp.Key).ToArray();

    /// <inheritdoc />
    public bool CanCreateSessions => true;

    /// <inheritdoc />
    public ISessionRun? ForegroundSession =>
        _sessions.TryGetValue(ISessionManager.ForegroundKey, out var mounted) ? mounted.Session : null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Keys => _sessions.Keys.ToArray();

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

    // ── Internal methods for ProgressRun ──────────────────────────────

    /// <summary>
    ///     创建前台会话并自动挂载。若已有前台会话，先销毁旧的。
    /// </summary>
    internal ISessionRun CreateForegroundSession(string levelId, ISndSceneHost sceneHost)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            throw new ArgumentException("Level id cannot be null or whitespace.", nameof(levelId));
        ArgumentNullException.ThrowIfNull(sceneHost);

        ValidateLevelIdUnique(levelId, ISessionManager.ForegroundKey);

        var sessionParams = new SessionParameters(levelId, new Blackboard.Blackboard(), sceneHost, true);
        var session = new SessionRun(_managerRuntime, sessionParams);
        MountInternal(ISessionManager.ForegroundKey, session, false);
        return session;
    }

    /// <summary>
    ///     创建前台会话，从 payload 恢复状态，并自动挂载。若已有前台会话，先销毁旧的。
    /// </summary>
    internal ISessionRun CreateForegroundFromPayload(string levelId, ISndSceneHost sceneHost, LevelPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var session = CreateForegroundSession(levelId, sceneHost);
        // session is always SessionRun (created by CreateForegroundSession → MountInternal),
        // so we can directly access it through the MountedSession.
        _sessions[ISessionManager.ForegroundKey].Session.LoadFromPayload(payload);
        return session;
    }

    /// <summary>
    ///     销毁当前前台会话（若存在）。
    /// </summary>
    internal void DestroyForeground() => DestroySession(ISessionManager.ForegroundKey);

    /// <summary>
    ///     将指定键的会话序列化为 <see cref="LevelPayload" />。
    /// </summary>
    internal LevelPayload SerializeSession(string key) => RequireMountedSession(key).Session.SerializeToPayload();

    /// <summary>
    ///     将指定键的会话状态持久化到 current/ 目录。
    /// </summary>
    internal void PersistSession(string key) => RequireMountedSession(key).Session.PersistLevelState();

    /// <summary>
    ///     从 <see cref="LevelPayload" /> 恢复指定键的会话状态。
    /// </summary>
    internal void LoadSessionFromPayload(string key, LevelPayload payload) =>
        RequireMountedSession(key).Session.LoadFromPayload(payload);

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
        return _sessions
            .Where(kvp => !string.Equals(kvp.Key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
            .Select(kvp => new KeyValuePair<string, ISessionRun>(kvp.Key, kvp.Value.Session))
            .ToArray();
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
        return new SessionRun(_managerRuntime, sessionParams);
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

        // If mounting foreground and old foreground exists, destroy old first.
        if (string.Equals(key, ISessionManager.ForegroundKey, StringComparison.Ordinal)
            && TryGetMountedSession(key) is not null)
            DestroyForeground();

        if (TryGetMountedSession(key) is not null)
            throw new InvalidOperationException($"A session with key '{key}' is already mounted.");

        _sessions[key] = new MountedSession(session, syncProcess);
        _managerRuntime.Logger.Log(LogLevel.Info, LogTag,
            $"Mounted session '{key}' (level: {session.LevelId}, syncProcess: {syncProcess}).");

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
        return _sessions.Keys.Where(key =>
                includeForeground || !string.Equals(key, ISessionManager.ForegroundKey, StringComparison.Ordinal))
            .ToArray();
    }

    private void DisposeMountedSession(string key, MountedSession mounted)
    {
        _managerRuntime.Logger.Log(LogLevel.Info, LogTag,
            $"Destroying session '{key}' (level: {mounted.Session.LevelId}).");

        mounted.Session.MountKey = null;
        mounted.Session.Dispose();
    }

    private sealed record MountedSession(SessionRun Session, bool SyncProcess);
}
