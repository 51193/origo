using System;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Declares a data key that an <see cref="ObserverStrategyBase" /> strategy
///     subscribes to on its mount target. Multiple declarations are allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ObserveDataAttribute : Attribute
{
    /// <summary>Declares an observed data key.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="dataKey" /> is null or whitespace.</exception>
    public ObserveDataAttribute(string dataKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataKey);
        DataKey = dataKey;
    }

    /// <summary>The data key whose changes trigger <see cref="ObserverStrategyBase.OnDataChanged" />.</summary>
    public string DataKey { get; }
}
