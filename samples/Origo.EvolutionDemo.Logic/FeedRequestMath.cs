using System;

namespace Origo.EvolutionDemo.Logic;

/// <summary>Pure arithmetic for the Feed button's queued food request.</summary>
public static class FeedRequestMath
{
    /// <summary>Adds the configured feed amount to the pending request count.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public static int Add(int currentRequest, int configuredAmount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentRequest);
        ArgumentOutOfRangeException.ThrowIfNegative(configuredAmount);
        return currentRequest + configuredAmount;
    }
}
