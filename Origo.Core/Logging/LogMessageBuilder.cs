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
    private readonly List<KeyValuePair<string, object?>> _context = [];
    private double? _elapsedMs;

    /// <summary>
    ///     Attaches an elapsed-time prefix (<c>[+N.NNms]</c>) to the built
    ///     message. The value must be a finite non-negative number.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="elapsedMs" /> is NaN, negative infinity,
    ///     positive infinity, or negative.
    /// </exception>
    public LogMessageBuilder SetElapsedMs(double elapsedMs)
    {
        if (double.IsNaN(elapsedMs) || double.IsInfinity(elapsedMs) || elapsedMs < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedMs), elapsedMs,
                "Elapsed milliseconds must be a finite non-negative number.");
        _elapsedMs = elapsedMs;
        return this;
    }

    /// <summary>Adds a contextual <c>key=value</c> pair, appended to the built message in insertion order.</summary>
    public LogMessageBuilder AddContext(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return this;

        var index = _context.FindIndex(kv => string.Equals(kv.Key, key, StringComparison.Ordinal));
        if (index >= 0)
            _context[index] = new KeyValuePair<string, object?>(key, value);
        else
            _context.Add(new KeyValuePair<string, object?>(key, value));

        return this;
    }

    /// <summary>Builds the final message string with the optional elapsed prefix and context pairs.</summary>
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
