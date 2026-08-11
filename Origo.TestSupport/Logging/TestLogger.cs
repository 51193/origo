using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Logging;

namespace Origo.TestSupport;

/// <summary>
///     Collects log records for test assertions. Writes are locked, so tests
///     may poll <see cref="SnapshotWarnings" /> while the code under test
///     writes from another thread; the public lists remain readable directly
///     after the writes have completed (the common single-threaded pattern).
/// </summary>
public sealed class TestLogger : ILogger
{
    private readonly Lock _lock = new();
    public readonly List<string> Debugs = [];
    public readonly List<string> Errors = [];
    public readonly List<string> Infos = [];
    public readonly List<string> Warnings = [];

    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    /// <summary>Gets a thread-safe snapshot of the warnings recorded so far.</summary>
    public IReadOnlyList<string> SnapshotWarnings()
    {
        lock (_lock)
        {
            return [.. Warnings];
        }
    }

    public void Log(LogLevel level, string tag, string message)
    {
        if (level < MinimumLevel)
            return;

        var entry = $"{tag}: {message}";
        lock (_lock)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debugs.Add(entry);
                    break;
                case LogLevel.Warning:
                    Warnings.Add(entry);
                    break;
                case LogLevel.Error:
                    Errors.Add(entry);
                    break;
                default:
                    Infos.Add(entry);
                    break;
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Debugs.Clear();
            Infos.Clear();
            Warnings.Clear();
            Errors.Clear();
        }
    }
}
