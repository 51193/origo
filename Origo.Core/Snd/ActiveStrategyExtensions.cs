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
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TOutput? InvokeStrategy<TInput, TOutput>(this ISndEntity entity, string strategyIndex, TInput input)
    {
        var serializedInput = JsonSerializer.Serialize(input, DefaultOptions);
        var result = entity.InvokeStrategy(strategyIndex, serializedInput);
        if (result is null)
            return default;
        var resultJson = result is string s ? s : JsonSerializer.Serialize(result, DefaultOptions);
        return JsonSerializer.Deserialize<TOutput>(resultJson, DefaultOptions);
    }

    public static TOutput? InvokeStrategy<TOutput>(this ISndEntity entity, string strategyIndex)
    {
        var result = entity.InvokeStrategy(strategyIndex);
        if (result is null)
            return default;
        var resultJson = result is string s ? s : JsonSerializer.Serialize(result, DefaultOptions);
        return JsonSerializer.Deserialize<TOutput>(resultJson, DefaultOptions);
    }

    public static bool EnsureStrategy(this ISndEntity entity, string dataKey, string strategyIndex)
    {
        var (found, current) = entity.TryGetData<string>(dataKey);
        if (found && !string.IsNullOrWhiteSpace(current))
            return false;

        entity.SetData(dataKey, strategyIndex);
        entity.AddStrategy(strategyIndex);
        return true;
    }
}
