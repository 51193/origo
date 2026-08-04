using System;
using System.Globalization;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Utility;

/// <summary>
///     String-to-typed value inference shared by console commands and
///     archetype loading. The parse order (int → long → float → bool →
///     string) is a single source of truth so every entry point converts
///     raw input identically: int is preferred over long, and long over
///     float, so integers beyond int range never degrade to float precision.
/// </summary>
internal static class ValueInference
{
    /// <summary>
    ///     Parses a raw string into the first matching typed value.
    ///     Falls back to the raw string itself when no type matches.
    /// </summary>
    internal static object Infer(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            return intValue;
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            return longValue;
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            return floatValue;
        if (bool.TryParse(raw, out var boolValue))
            return boolValue;
        return raw;
    }

    /// <summary>
    ///     Infers the value of <paramref name="raw" /> and stores it on the
    ///     entity under <paramref name="key" /> with a strongly typed
    ///     <c>SetData</c> call, preserving the inferred runtime type.
    /// </summary>
    internal static void SetData(ISndEntity entity, string key, string raw)
    {
        ArgumentNullException.ThrowIfNull(entity);
        switch (Infer(raw))
        {
            case int intValue:
                entity.SetData(key, intValue);
                break;
            case long longValue:
                entity.SetData(key, longValue);
                break;
            case float floatValue:
                entity.SetData(key, floatValue);
                break;
            case bool boolValue:
                entity.SetData(key, boolValue);
                break;
            default:
                entity.SetData(key, raw);
                break;
        }
    }

    /// <summary>
    ///     Infers the value of <paramref name="raw" /> and stores it on the
    ///     blackboard under <paramref name="key" /> with a strongly typed
    ///     <c>SetValue</c> call, preserving the inferred runtime type.
    /// </summary>
    internal static void SetBlackboard(IBlackboard blackboard, string key, string raw)
    {
        ArgumentNullException.ThrowIfNull(blackboard);
        switch (Infer(raw))
        {
            case int intValue:
                blackboard.SetValue(key, intValue);
                break;
            case long longValue:
                blackboard.SetValue(key, longValue);
                break;
            case float floatValue:
                blackboard.SetValue(key, floatValue);
                break;
            case bool boolValue:
                blackboard.SetValue(key, boolValue);
                break;
            default:
                blackboard.SetValue(key, raw);
                break;
        }
    }
}
