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

    /// <summary>The unique index of the strategy in the strategy pool.</summary>
    public string Index { get; }

    /// <summary>Indices of lifecycle strategies that must execute after this strategy.</summary>
    public string[] Before { get; set; } = [];

    /// <summary>Indices of lifecycle strategies that must execute before this strategy.</summary>
    public string[] After { get; set; } = [];
}
