using System;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Runtime;

namespace Origo.Core.Scheduling;

/// <summary>
///     基于 ConcurrentActionQueue 的简单调度器实现。
///     宿主环境负责在合适的时间调用 Tick 执行排队的动作。
/// </summary>
internal sealed class ActionScheduler(ILogger logger) : IScheduler
{
    private readonly ConcurrentActionQueue _queue = new ConcurrentActionQueue(logger);

    public void Enqueue(Action action) => _queue.Enqueue(action);

    /// <summary>
    ///     由宿主循环调用，用于执行已排队的所有动作。
    /// </summary>
    public int Tick() => _queue.ExecuteAll();

    public void Clear() => _queue.Clear();
}
