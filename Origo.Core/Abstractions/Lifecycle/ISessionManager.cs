using System.Collections.Generic;

namespace Origo.Core.Abstractions.Lifecycle;

/// <summary>
///     Session manager interface, fully manages the lifecycle of all
///     <see cref="ISessionRun" /> instances.
/// </summary>
public interface ISessionManager
{
    /// <summary>Reserved key for the foreground session.</summary>
    const string ForegroundKey = "__foreground__";

    /// <summary>Whether sessions can currently be created. Returns false for the Empty Session Manager.</summary>
    bool CanCreateSessions { get; }

    /// <summary>Current foreground session; null when no foreground session is active.</summary>
    ISessionRun? ForegroundSession { get; }

    /// <summary>Get the list of keys for all mounted sessions.</summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>Get a session by key.</summary>
    ISessionRun? TryGet(string key);

    /// <summary>Check whether a session with the given key exists.</summary>
    bool Contains(string key);

    /// <summary>
    ///     Create a background level session and auto-mount it to the manager.
    ///     Uses a pure in-memory scene host implementation, independent of the engine adapter layer.
    /// </summary>
    ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false);

    /// <summary>
    ///     Destroy the session with the given key. Idempotent: destroying a
    ///     key that is not mounted (including a blank key) is a no-op, matching
    ///     <see cref="Contains" /> / <see cref="TryGet" /> query semantics and
    ///     keeping framework teardown paths (foreground switch, Clear) simple.
    /// </summary>
    void DestroySession(string key);

    /// <summary>
    ///     Perform frame updates for all sessions configured to participate in Process.
    /// </summary>
    void ProcessAllSessions(double delta, bool includeForeground = false);

    /// <summary>
    ///     Harvest entities marked for removal across all sessions (including foreground):
    ///     tear down observer bindings, fire BeforeDead hooks, then physically remove.
    ///     Called once per frame-end to give foreground and background sessions identical
    ///     kill-pending semantics — the foreground is no longer special-cased.
    /// </summary>
    void KillPendingAllSessions();
}
