using System;
using System.Collections.Generic;
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

    private readonly List<Action> _actionQueue = [];
    private readonly object _lock = new();
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
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_lock)
        {
            _actionQueue.Add(action);
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
            if (executeBatchCount >= _maxReentrantDrainDepth)
                throw new InvalidOperationException(
                    $"ConcurrentActionQueue exceeded max re-entrant drain depth ({_maxReentrantDrainDepth}).");

            List<Action> currentBatch;
            lock (_lock)
            {
                if (_actionQueue.Count == 0) break;
                currentBatch = [.. _actionQueue];
                _actionQueue.Clear();
            }

            foreach (var action in currentBatch)
                try
                {
                    action.Invoke();
                    executeCount++;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, nameof(ConcurrentActionQueue),
                        new LogMessageBuilder().Build($"Deferred action execution failed: {ex.Message}"));
                    throw;
                }

            executeBatchCount++;
        }

        return executeCount;
    }

    /// <summary>
    ///     Clears all pending actions in the queue.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _actionQueue.Clear();
        }
    }
}
