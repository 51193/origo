namespace Origo.Core.Snd;

/// <summary>
///     Owns the shutdown lifecycle state transitions for a single
///     <see cref="SndContext" />.
///     <para>
///         Direct <c>Shutdown()</c> calls and workflow teardown both need the
///         same decisions: whether a nested shutdown request is deferred to the
///         current teardown owner, how a successful teardown commits the
///         terminal flag, and how a failed cleanup records only the retryable
///         cleanup steps. Keeping these flags and transitions in one place
///         prevents the two teardown paths from drifting apart.
///     </para>
/// </summary>
internal sealed class SndShutdownState
{
    /// <summary>True once shutdown has completed and no lifecycle operation may start.</summary>
    internal bool IsTerminal { get; private set; }

    /// <summary>True while a shutdown attempt owns progress/scene teardown.</summary>
    internal bool IsAttemptInProgress { get; private set; }

    /// <summary>
    ///     True when a previous shutdown attempt failed after progress teardown;
    ///     only the idempotent cleanup steps remain retryable.
    /// </summary>
    internal bool IsCleanupPending { get; private set; }

    /// <summary>Whether the pending cleanup retry must still delete <c>current/</c>.</summary>
    internal bool RetryCurrentDelete { get; private set; }

    private bool _nestedShutdownRequested;

    /// <summary>Start a teardown attempt. The caller must pair this with <see cref="EndAttempt" />.</summary>
    internal void BeginAttempt() => IsAttemptInProgress = true;

    /// <summary>Release ownership of the current teardown attempt.</summary>
    internal void EndAttempt() => IsAttemptInProgress = false;

    /// <summary>
    ///     Record a nested shutdown request from a quit hook. The current
    ///     attempt owner decides whether it commits the terminal state.
    /// </summary>
    internal void RequestNestedShutdown() => _nestedShutdownRequested = true;

    /// <summary>
    ///     Consume a nested request, clearing it so a later attempt cannot
    ///     observe a stale request.
    /// </summary>
    internal bool ConsumeNestedRequest()
    {
        if (!_nestedShutdownRequested)
            return false;

        _nestedShutdownRequested = false;
        return true;
    }

    /// <summary>Clear a nested request without consuming it as a shutdown decision.</summary>
    internal void ClearNestedRequest() => _nestedShutdownRequested = false;

    /// <summary>
    ///     Commit the terminal state after a successful teardown. Retry records
    ///     are cleared because nothing remains to clean up.
    /// </summary>
    internal void CommitTerminalState()
    {
        IsCleanupPending = false;
        RetryCurrentDelete = false;
        IsTerminal = true;
    }

    /// <summary>
    ///     Record a failed teardown as a retryable cleanup. The terminal flag is
    ///     not committed, and a nested request from the failed attempt is
    ///     cleared so a later workflow cannot consume stale intent.
    /// </summary>
    /// <param name="retryCurrentDelete">
    ///     Whether the retry must still attempt the idempotent <c>current/</c>
    ///     deletion.
    /// </param>
    internal void ScheduleCleanupRetry(bool retryCurrentDelete)
    {
        IsCleanupPending = true;
        RetryCurrentDelete = retryCurrentDelete;
        _nestedShutdownRequested = false;
    }
}
