using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Origo.Core.Logging;

public sealed class LogMessageBuilder
{
    private readonly Dictionary<string, object?> _prefixContext = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _suffixContext = new(StringComparer.Ordinal);
    private double? _elapsedMs;

    public LogMessageBuilder SetElapsedMs(double elapsedMs)
    {
        _elapsedMs = elapsedMs;
        return this;
    }

    public LogMessageBuilder AddPrefix(string key, object? value)
    {
        if (!string.IsNullOrWhiteSpace(key) && value is not null) _prefixContext[key] = value;
        return this;
    }

    public LogMessageBuilder AddSuffix(string key, object? value)
    {
        if (!string.IsNullOrWhiteSpace(key) && value is not null) _suffixContext[key] = value;
        return this;
    }

    public string Build(string message)
    {
        var builder = new StringBuilder();
        if (_elapsedMs.HasValue) builder.Append("[+").Append(Math.Round(_elapsedMs.Value, 2)).Append("ms] ");

        if (_prefixContext.Count > 0)
            builder.Append(string.Join(", ", _prefixContext.Select(kv => $"{kv.Key}={kv.Value}"))).Append(" | ");

        builder.Append(message);

        if (_suffixContext.Count > 0)
            builder.Append(" | ").Append(string.Join(", ", _suffixContext.Select(kv => $"{kv.Key}={kv.Value}")));

        return builder.ToString();
    }
}
