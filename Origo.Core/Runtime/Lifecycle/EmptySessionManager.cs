using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Empty session manager (Null Object pattern), returned as a placeholder when no ProgressRun has been established.
///     Holds no sessions. Callers should check <see cref="CanCreateSessions" /> first;
///     creating a session without an active ProgressRun throws <see cref="InvalidOperationException" />.
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
