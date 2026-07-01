using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     空会话管理器（Null Object 模式），在 ProgressRun 尚未建立时作为占位实例返回。
///     不持有任何会话。调用方应先检查 <see cref="CanCreateSessions" />，
///     在无活动 ProgressRun 时创建会话将返回 null。
/// </summary>
internal sealed class EmptySessionManager : ISessionManager
{
    internal static readonly EmptySessionManager Instance = new();

    private EmptySessionManager()
    {
    }

    public bool CanCreateSessions => false;

    public ISessionRun? ForegroundSession => null;

    public IReadOnlyCollection<string> Keys => [];

    public ISessionRun? TryGet(string key) => null;

    public bool Contains(string key) => false;

    public ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false)
    {
        throw new InvalidOperationException(
            "No active ProgressRun. Cannot create sessions before loading a save or starting a new game.");
    }

    public void DestroySession(string key)
    {
        // No-op: no sessions to destroy.
    }

    public void ProcessAllSessions(double delta, bool includeForeground = false)
    {
        // No-op: no sessions to process.
    }

    public void KillPendingAllSessions()
    {
        // No-op: no sessions to sweep.
    }
}
