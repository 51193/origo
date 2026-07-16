using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Origo.Core.Logging;

/// <summary>
///     Fluent builder for structured log messages with optional elapsed
///     time and contextual key-value pairs.
/// </summary>
public sealed class LogMessageBuilder
{
    private readonly Dictionary<string, object?> _context = new(StringComparer.Ordinal);
    private double? _elapsedMs;

    public LogMessageBuilder SetElapsedMs(double elapsedMs)
    {
        _elapsedMs = elapsedMs;
        return this;
    }

    public LogMessageBuilder AddContext(string key, object? value)
    {
        if (!string.IsNullOrWhiteSpace(key)) _context[key] = value;
        return this;
    }

    public string Build(string message)
    {
        var builder = new StringBuilder();
        if (_elapsedMs.HasValue)
        {
            var rounded = Math.Round(_elapsedMs.Value, 2);
            builder.Append(CultureInfo.InvariantCulture, $"[+{rounded:F2}ms] ");
        }

        builder.Append(message);

        if (_context.Count > 0)
            builder.Append(" | ").Append(string.Join(", ", _context.Select(kv => $"{kv.Key}={kv.Value}")));

        return builder.ToString();
    }
}
