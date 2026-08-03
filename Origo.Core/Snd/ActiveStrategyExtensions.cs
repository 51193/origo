using System;
using System.Text.Json;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd;

/// <summary>
///     Generic extension methods for type-safe active strategy invocation.
///     Eliminates repetitive <c>JsonSerializer.Serialize(new {...})</c> /
///     <c>JsonSerializer.Deserialize</c> boilerplate in caller code.
/// </summary>
public static class ActiveStrategyExtensions
{
    private static readonly JsonSerializerOptions _defaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TOutput? InvokeStrategy<TInput, TOutput>(this ISndEntity entity, string strategyIndex, TInput input)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var serializedInput = JsonSerializer.Serialize(input, _defaultOptions);
        var result = entity.InvokeStrategy(strategyIndex, serializedInput);
        return DeserializeResult<TOutput>(result);
    }

    public static TOutput? InvokeStrategy<TOutput>(this ISndEntity entity, string strategyIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var result = entity.InvokeStrategy(strategyIndex);
        return DeserializeResult<TOutput>(result);
    }

    /// <summary>
    ///     Deserializes a strategy result into <typeparamref name="TOutput" />.
    ///     String results are treated as JSON; when they are not valid JSON
    ///     and the expected output is a string, the raw string is returned
    ///     as-is so strategies that return bare strings (e.g. "ok") remain
    ///     callable without throwing opaque JsonExceptions.
    /// </summary>
    private static TOutput? DeserializeResult<TOutput>(object? result)
    {
        if (result is null)
            return default;

        var resultJson = result is string s ? s : JsonSerializer.Serialize(result, _defaultOptions);
        try
        {
            return JsonSerializer.Deserialize<TOutput>(resultJson, _defaultOptions);
        }
        catch (JsonException) when (typeof(TOutput) == typeof(string))
        {
            return (TOutput)(object)resultJson;
        }
    }

    public static bool EnsureStrategy(this ISndEntity entity, string dataKey, string strategyIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var (found, current) = entity.TryGetData<string>(dataKey);
        if (found && !string.IsNullOrWhiteSpace(current))
            return false;

        entity.SetData(dataKey, strategyIndex);
        entity.AddStrategy(strategyIndex);
        return true;
    }
}
