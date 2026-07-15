using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;

namespace Origo.TestSupport;

public sealed class TestLogger : ILogger
{
    public readonly List<string> Debugs = [];
    public readonly List<string> Errors = [];
    public readonly List<string> Infos = [];
    public readonly List<string> Warnings = [];

    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public void Log(LogLevel level, string tag, string message)
    {
        if (level < MinimumLevel) return;
        switch (level)
        {
            case LogLevel.Debug:
                Debugs.Add($"{tag}: {message}");
                break;
            case LogLevel.Warning:
                Warnings.Add($"{tag}: {message}");
                break;
            case LogLevel.Error:
                Errors.Add($"{tag}: {message}");
                break;
            default:
                Infos.Add($"{tag}: {message}");
                break;
        }
    }

    public void Clear()
    {
        Debugs.Clear();
        Infos.Clear();
        Warnings.Clear();
        Errors.Clear();
    }
}
