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
        if (result is null)
            return default;
        var resultJson = result is string s ? s : JsonSerializer.Serialize(result, _defaultOptions);
        return JsonSerializer.Deserialize<TOutput>(resultJson, _defaultOptions);
    }

    public static TOutput? InvokeStrategy<TOutput>(this ISndEntity entity, string strategyIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var result = entity.InvokeStrategy(strategyIndex);
        if (result is null)
            return default;
        var resultJson = result is string s ? s : JsonSerializer.Serialize(result, _defaultOptions);
        return JsonSerializer.Deserialize<TOutput>(resultJson, _defaultOptions);
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
