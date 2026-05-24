using System;
using System.Collections.Generic;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     空会话管理器（Null Object 模式），在 ProgressRun 尚未建立时作为占位实例返回。
///     不持有任何会话，所有创建操作均抛出 <see cref="InvalidOperationException" />。
/// </summary>
internal sealed class EmptySessionManager : ISessionManager
{
    internal static readonly EmptySessionManager Instance = new();

    private EmptySessionManager()
    {
    }

    public ISessionRun? ForegroundSession => null;

    public IReadOnlyCollection<string> Keys => Array.Empty<string>();

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
}
