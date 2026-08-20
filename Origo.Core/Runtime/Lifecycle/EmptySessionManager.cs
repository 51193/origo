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

    /// <inheritdoc/>
    public bool CanCreateSessions => false;

    /// <inheritdoc/>
    public ISessionRun? ForegroundSession => null;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Keys => [];

    /// <inheritdoc/>
    public ISessionRun? TryGet(string key) => null;

    /// <inheritdoc/>
    public bool Contains(string key) => false;

    /// <inheritdoc/>
    public ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false)
    {
        throw new InvalidOperationException(
            "No active ProgressRun. Cannot create sessions before loading a save or starting a new game.");
    }

    /// <inheritdoc/>
    public void DestroySession(string key)
    {
        // No-op: no sessions to destroy.
    }

    /// <inheritdoc/>
    public void ProcessAllSessions(double delta, bool includeForeground = false)
    {
        // No-op: no sessions to process.
    }

    /// <inheritdoc/>
    public void KillPendingAllSessions()
    {
        // No-op: no sessions to sweep.
    }
}
