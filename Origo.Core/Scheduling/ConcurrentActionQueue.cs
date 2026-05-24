using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.Scheduling;

/// <summary>
///     线程安全的延迟执行队列，可在调度器或主循环中按批次执行入队的 Action。
///     仅作为 Core 内部实现细节，对程序集外不可见。
/// </summary>
internal class ConcurrentActionQueue
{
    /// <summary>
    ///     Guard against infinite synchronous re-queue (action enqueues another that runs in the same drain).
    /// </summary>
    private const int MaxReentrantDrainDepth = 100;

    private readonly List<Action> _actionQueue = [];
    private readonly object _lock = new();
    private readonly ILogger _logger;

    /// <summary>
    ///     创建一个新的并发动作队列。
    /// </summary>
    /// <param name="logger">日志接口，用于记录异常情况。</param>
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
    ///     入队一个待执行的动作。
    /// </summary>
    /// <param name="action">要执行的动作。</param>
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_lock)
        {
            _actionQueue.Add(action);
        }
    }

    /// <summary>
    ///     以批次方式执行队列中的所有动作。
    ///     如果在某批执行过程中又有新的动作入队，将在下一轮批处理中继续执行。
    /// </summary>
    /// <returns>本次调用中执行的总动作数量。</returns>
    public int ExecuteAll()
    {
        var executeCount = 0;
        var executeBatchCount = 0;

        while (true)
        {
            if (executeBatchCount >= MaxReentrantDrainDepth)
                throw new InvalidOperationException(
                    $"ConcurrentActionQueue exceeded max re-entrant drain depth ({MaxReentrantDrainDepth}).");

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
    ///     清空队列中的所有待执行动作。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _actionQueue.Clear();
        }
    }
}
