using System.Threading;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Scheduling;

/// <summary>
///     Thread-safe deferred execution queue that executes enqueued actions in batches
///     within a scheduler or main loop. Internal Core implementation detail, not visible
///     outside the assembly.
///     <para>
///         <b>Fail-fast semantics:</b> when a single deferred action throws an exception,
///         it is logged and immediately propagated via <c>throw</c>, causing all remaining
///         actions in the current batch to be <em>abandoned</em>. Callers must ensure
///         enqueued actions are robust. If partial-failure-continue semantics are needed,
///         catch exceptions within the action itself.
///     </para>
/// </summary>
internal class ConcurrentActionQueue
{
    /// <summary>
    ///     Guard against infinite synchronous re-queue (action enqueues another that runs in the same drain).
    /// </summary>
    private const int _maxReentrantDrainDepth = 100;

    private readonly List<QueuedAction> _actionQueue = [];
    private readonly Lock _lock = new();
    private readonly ILogger _logger;

    /// <summary>
    ///     Creates a new concurrent action queue.
    /// </summary>
    /// <param name="logger">Logger instance for recording exceptions.</param>
    public ConcurrentActionQueue(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Returns the number of actions currently queued for execution.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _actionQueue.Count;
            }
        }
    }

    /// <summary>
    ///     Enqueues an action for deferred execution.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Enqueue(Action action) => Enqueue(action, null);

    /// <summary>
    ///     Enqueues an action for deferred execution with a callback that runs
    ///     when the action is discarded without being executed (fail-fast batch
    ///     abandonment or an explicit <see cref="Clear" />). The callback is not
    ///     invoked when the action itself runs, even if it throws.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="onDiscard">Optional cleanup for an abandoned action.</param>
    public void Enqueue(Action action, Action? onDiscard)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_lock)
        {
            _actionQueue.Add(new QueuedAction(action, onDiscard));
        }
    }

    /// <summary>
    ///     Executes all queued actions in batched mode.
    ///     If new actions are enqueued during execution of a batch, they will be processed
    ///     in the next round of batch processing.
    ///     <para>
    ///         <b>Note:</b> uses fail-fast strategy. If any action throws an exception,
    ///         it is re-thrown immediately after logging, and remaining unexecuted actions
    ///         in the queue are discarded. Callers should ensure enqueued actions do not
    ///         throw unhandled exceptions, or catch them within the action itself.
    ///     </para>
    /// </summary>
    /// <returns>Total number of actions executed in this call (including those successfully
    /// executed before an exception was thrown).</returns>
    public int ExecuteAll()
    {
        var executeCount = 0;
        var executeBatchCount = 0;

        while (true)
        {
            List<QueuedAction> currentBatch;
            lock (_lock)
            {
                if (_actionQueue.Count == 0) break;
                if (executeBatchCount >= _maxReentrantDrainDepth)
                    throw new InvalidOperationException(
                        $"ConcurrentActionQueue exceeded max re-entrant drain depth ({_maxReentrantDrainDepth}).");
                currentBatch = [.. _actionQueue];
                _actionQueue.Clear();
            }

            for (var i = 0; i < currentBatch.Count; i++)
            {
                try
                {
                    currentBatch[i].Action.Invoke();
                    executeCount++;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, nameof(ConcurrentActionQueue),
                        new LogMessageBuilder().Build($"Deferred action execution failed: {ex.Message}"));
                    var discardFailures = InvokeDiscardForRange(currentBatch, i + 1);
                    if (discardFailures.Count == 0)
                        throw;
                    throw new AggregateException(
                        "Deferred action failed and discard cleanup also failed; see inner exceptions.",
                        [ex, .. discardFailures]);
                }
            }

            executeBatchCount++;
        }

        return executeCount;
    }

    /// <summary>
    ///     Clears all pending actions in the queue, running each discarded
    ///     action's discard callback.
    /// </summary>
    public void Clear()
    {
        List<QueuedAction> discarded;
        lock (_lock)
        {
            discarded = [.. _actionQueue];
            _actionQueue.Clear();
        }

        var failures = InvokeDiscard(discarded);
        if (failures.Count == 0)
            return;
        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException(
            "Multiple discard cleanup callbacks failed while clearing the queue; see inner exceptions.",
            failures);
    }

    private static List<Exception> InvokeDiscardForRange(List<QueuedAction> batch, int startIndex)
    {
        var failures = new List<Exception>();
        for (var i = startIndex; i < batch.Count; i++)
        {
            try
            {
                InvokeDiscard(batch[i]);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        return failures;
    }

    private static List<Exception> InvokeDiscard(List<QueuedAction> items)
    {
        var failures = new List<Exception>();
        foreach (var item in items)
        {
            try
            {
                InvokeDiscard(item);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        return failures;
    }

    private static void InvokeDiscard(QueuedAction item) => item.OnDiscard?.Invoke();

    private sealed record QueuedAction(Action Action, Action? OnDiscard);
}
