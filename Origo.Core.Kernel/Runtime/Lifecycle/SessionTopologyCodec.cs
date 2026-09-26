using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Save;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Centralized codec for the session topology string format stored in
///     <see cref="Save.WellKnownKeys.SessionTopology" />.
///     Format: <c>key=levelId=syncProcess,key=levelId=syncProcess,...</c>.
/// </summary>
internal static class SessionTopologyCodec
{
    private const char _entrySeparator = ',';
    private const char _fieldSeparator = '=';
    private const int _requiredFieldCount = 3;

    /// <summary>
    ///     Serializes a single topology entry into the canonical string form.
    /// </summary>
    public static string Serialize(string key, string levelId, bool syncProcess) =>
        $"{key}{_fieldSeparator}{levelId}{_fieldSeparator}{(syncProcess ? "true" : "false")}";

    /// <summary>
    ///     Joins multiple serialized entries into a single topology string.
    /// </summary>
    public static string Join(IEnumerable<string> entries) => string.Join(_entrySeparator, entries);

    /// <summary>
    ///     Parses a topology string into a list of <see cref="SessionDescriptor" />.
    ///     Throws <see cref="InvalidOperationException" /> on malformed entries (explicit failure).
    /// </summary>
    public static List<SessionDescriptor> Parse(string raw)
    {
        var list = new List<SessionDescriptor>();
        var entries = raw.Split(_entrySeparator);
        foreach (var entry in entries)
        {
            var parts = entry.Split(_fieldSeparator);
            if (parts.Length != _requiredFieldCount)
                throw new InvalidOperationException(
                    $"Malformed session topology entry '{entry}': expected exactly 'key=levelId=syncProcess' " +
                    $"(found {parts.Length} fields).");

            var key = parts[0];
            var levelId = parts[1];
            if (!bool.TryParse(parts[2], out var sync))
                throw new InvalidOperationException(
                    $"Malformed session topology entry '{entry}': syncProcess must be 'true' or 'false'.");

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(levelId))
                throw new InvalidOperationException(
                    $"Session topology entry '{entry}' has empty key or levelId.");

            list.Add(new SessionDescriptor(key, levelId, sync));
        }

        return list;
    }

    /// <summary>
    ///     Parses topology raw string and extracts the foreground level id.
    /// </summary>
    public static string ExtractForegroundLevelId(
        string raw,
        string foregroundKey = ISessionManager.ForegroundKey)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                $"Missing required '{WellKnownKeys.SessionTopology}' in progress blackboard.");
        return ExtractForegroundLevelId(Parse(raw), foregroundKey);
    }

    /// <summary>
    ///     Extracts the foreground level id from parsed topology descriptors.
    /// </summary>
    public static string ExtractForegroundLevelId(
        IReadOnlyList<SessionDescriptor> descriptors,
        string foregroundKey = ISessionManager.ForegroundKey)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (string.IsNullOrWhiteSpace(foregroundKey))
            throw new ArgumentException("Foreground key cannot be null or whitespace.", nameof(foregroundKey));

        string? foregroundLevelId = null;
        foreach (var descriptor in descriptors)
        {
            if (!string.Equals(descriptor.Key, foregroundKey, StringComparison.Ordinal))
                continue;
            if (foregroundLevelId is not null)
                throw new InvalidOperationException(
                    $"Session topology contains duplicate foreground key '{foregroundKey}'.");
            foregroundLevelId = descriptor.LevelId;
        }

        if (string.IsNullOrWhiteSpace(foregroundLevelId))
            throw new InvalidOperationException(
                $"Session topology missing required foreground key '{foregroundKey}'.");
        return foregroundLevelId;
    }

    /// <summary>Lightweight descriptor for a session topology entry.</summary>
    internal readonly record struct SessionDescriptor(string Key, string LevelId, bool SyncProcess);
}
