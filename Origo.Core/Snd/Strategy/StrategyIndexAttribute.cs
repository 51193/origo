using System;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     Optional strategy index declaration. During the auto-discovery phase, this attribute
///     is read first to avoid instantiating strategies solely for reading their Index.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StrategyIndexAttribute : Attribute
{
    /// <summary>Declares the strategy's unique index in the pool.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="index" /> is null or whitespace.</exception>
    public StrategyIndexAttribute(string index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);
        Index = index;
    }

    /// <summary>
    ///     Default execution priority for strategy lifecycle hooks.
    ///     Chosen as a midpoint (centered between 0 and ~12410) to leave
    ///     equal headroom for higher- and lower-priority strategies.
    /// </summary>
    public const int DefaultPriority = 6205;

    /// <summary>The unique index of the strategy in the strategy pool.</summary>
    public string Index { get; }

    /// <summary>Execution priority for lifecycle hooks; defaults to <see cref="DefaultPriority" />.</summary>
    public int Priority { get; set; } = DefaultPriority;
}
