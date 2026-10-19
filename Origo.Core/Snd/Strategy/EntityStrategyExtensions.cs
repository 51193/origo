using System;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Extension methods for <see cref="ISndEntity" /> strategy management.
/// </summary>
public static class EntityStrategyExtensions
{
    /// <summary>
    ///     Ensures a replaceable resident strategy is present on the entity unless an
    ///     implementation index is already configured. Reads <paramref name="implKey" />
    ///     from entity data: a non-blank configured index (for example one set by a
    ///     template) is preserved and the method returns <c>false</c>, so the caller
    ///     that configured it is responsible for mounting that strategy. Otherwise
    ///     <paramref name="defaultStrategyIndex" /> is mounted through
    ///     <see cref="ActiveStrategyExtensions.EnsureStrategy" />.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="implKey">
    ///     The data key storing the configured strategy index. Also used as the deduplication marker.
    /// </param>
    /// <param name="defaultStrategyIndex">The default strategy index when no override is configured.</param>
    /// <returns>
    ///     <c>true</c> if the default strategy was newly added by this call;
    ///     <c>false</c> if a configured override is preserved or the default was
    ///     already present.
    /// </returns>
    public static bool EnsureReplaceableStrategy(this ISndEntity entity, string implKey, string defaultStrategyIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(implKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultStrategyIndex);

        var (foundConfigured, configuredIndex) = entity.TryGetData<string>(implKey);
        var effectiveIndex = (foundConfigured && !string.IsNullOrWhiteSpace(configuredIndex))
            ? configuredIndex
            : defaultStrategyIndex;

        return ActiveStrategyExtensions.EnsureStrategy(entity, implKey, effectiveIndex);
    }
}
