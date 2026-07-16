using System;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Runtime;

namespace Origo.Core.Scheduling;

/// <summary>
///     Simple scheduler implementation based on ConcurrentActionQueue.
///     The host environment is responsible for calling Tick to execute queued actions
///     at appropriate times.
/// </summary>
internal sealed class ActionScheduler(ILogger logger) : IScheduler
{
    private readonly ConcurrentActionQueue _queue = new(logger);

    public void Enqueue(Action action) => _queue.Enqueue(action);

    /// <summary>
    ///     Called by the host loop to execute all queued actions.
    /// </summary>
    public int Tick() => _queue.ExecuteAll();

    public void Clear() => _queue.Clear();
}
