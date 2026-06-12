using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;

namespace Origo.Core.DataSource;

/// <summary>
///     解析按行的 <c>key: value</c> 文本（忽略空行与 <c>#</c> 注释行）。
/// </summary>
internal static class KeyValueFileParser
{
    private static readonly char[] LineSeparators = ['\r', '\n'];

    public static Dictionary<string, string> Parse(
        string? content,
        string sourceName,
        bool strict,
        ILogger logger,
        bool allowEmptyValues = false)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(content))
            return result;

        var lines = content.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            ProcessLine(line, sourceName, strict, allowEmptyValues, logger, result);
        }

        return result;
    }

    private static void ProcessLine(
        string line,
        string sourceName,
        bool strict,
        bool allowEmptyValues,
        ILogger logger,
        Dictionary<string, string> result)
    {
        var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            if (strict)
                throw new FormatException(
                    $"Invalid line '{line}' in '{sourceName}'. Expected 'key: value'.");

            logger.Log(LogLevel.Warning, nameof(KeyValueFileParser),
                new LogMessageBuilder().AddContext("filePath", sourceName)
                    .Build($"Invalid line '{line}'. Expected 'key: value'."));
            return;
        }

        var key = parts[0];
        var value = parts[1];
        var invalidEmptyKey = key.Length == 0;
        var invalidEmptyValue = value.Length == 0 && !allowEmptyValues;

        if (invalidEmptyKey || invalidEmptyValue)
        {
            if (strict)
                throw new FormatException(
                    $"Invalid line '{line}' in '{sourceName}'. Empty key or value.");

            logger.Log(LogLevel.Warning, nameof(KeyValueFileParser),
                new LogMessageBuilder().AddContext("filePath", sourceName)
                    .Build($"Invalid line '{line}'. Empty key or value."));
            return;
        }

        if (result.ContainsKey(key))
            logger.Log(LogLevel.Warning, nameof(KeyValueFileParser),
                new LogMessageBuilder().AddContext("filePath", sourceName).AddContext("key", key)
                    .Build($"Duplicate key '{key}' in '{sourceName}'; later value wins."));

        result[key] = value;
    }
}
