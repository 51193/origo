using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Strategy;
using System.Text.Json;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Base class for active strategies that exchange JSON payloads with
///     callers through the generic <c>InvokeStrategy&lt;TInput, TOutput&gt;</c>
///     extension methods.
///     <para>
///         The base class owns the serialization contract: the raw input
///         string is deserialized to <typeparamref name="TInput" /> and the
///         <see cref="Execute" /> result is serialized back to a JSON string
///         (which the generic extensions then deserialize to the expected
///         output type). Subclasses implement strongly-typed logic only and
///         return plain objects (strings, bools, POCOs, ...) — never
///         pre-serialized JSON. Use <see cref="ActiveStrategyResults" /> for
///         the conventional success/error strings.
///     </para>
///     <para>
///         Invalid or non-JSON input yields the conventional error result
///         <c>"err:Invalid request"</c> instead of throwing. This is an
///         explicit, caller-observable failure at the strategy service
///         boundary — not a silent fallback.
///     </para>
/// </summary>
/// <typeparam name="TInput">The strongly-typed input payload type.</typeparam>
public abstract class ActiveStrategyJsonBase<TInput> : ActiveStrategyBase
{
    /// <summary>
    ///     Executes the strategy with a strongly-typed input. The input is
    ///     annotated non-nullable to match override signatures (an
    ///     unconstrained <c>TInput?</c> compiles to plain <c>TInput</c> in
    ///     IL, so concrete nullable overrides would not match). Null input
    ///     (invoked without input) surfaces as <c>default(TInput)</c>;
    ///     reference-type inputs may therefore be null at runtime.
    /// </summary>
    /// <param name="entity">The entity the strategy is invoked on.</param>
    /// <param name="ctx">The SND context.</param>
    /// <param name="input">The deserialized input, or default when the JSON was null.</param>
    /// <returns>The result object to serialize back to the caller.</returns>
    protected abstract object? Execute(ISndEntity entity, ISndContext ctx, TInput input);

    /// <summary>
    ///     Parses the raw JSON input string, dispatches to
    ///     <see cref="Execute" />, and serializes the result. A null input
    ///     (strategies invoked without input) yields
    ///     <c>default(TInput)</c>.
    /// </summary>
    public sealed override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
    {
        if (input is null)
        {
            var result = Execute(entity, ctx, default!);
            return JsonSerializer.Serialize(result);
        }

        if (input is not string json)
            return JsonSerializer.Serialize(ActiveStrategyResults.Err("Invalid request"));

        TInput? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TInput>(json);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(ActiveStrategyResults.Err("Invalid request"));
        }

        var executeResult = Execute(entity, ctx, parsed ?? default!);
        return JsonSerializer.Serialize(executeResult);
    }
}
