using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Snd;

namespace Origo.Core.Random;

/// <summary>
///     Persistent random number generator that stores its state in a progress blackboard.
///     Initialized from a string seed; each invocation atomically reads, advances,
///     and writes back the state. Deterministic across sessions for the same seed.
/// </summary>
public sealed class PersistentRandom(IBlackboard blackboard, string? state1Key = null, string? state2Key = null)
{
    private const string _defaultState1Key = "rand.state1";
    private const string _defaultState2Key = "rand.state2";

    private readonly IBlackboard _blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
    private readonly string _state1Key = string.IsNullOrWhiteSpace(state1Key) ? _defaultState1Key : state1Key;
    private readonly string _state2Key = string.IsNullOrWhiteSpace(state2Key) ? _defaultState2Key : state2Key;

    /// <summary>Initializes the random state from a string seed, overwriting any existing state.</summary>
    public bool InitSeed(string seed)
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed(seed);
        _blackboard.SetValue(_state1Key, s0);
        _blackboard.SetValue(_state2Key, s1);
        return true;
    }

    /// <summary>Advances the generator once and returns the raw 32-bit value; <c>false</c> when state is not initialized.</summary>
    public bool TryNextInt32(out int value)
    {
        value = 0;
        var (f1, s1) = _blackboard.TryGet<ulong>(_state1Key);
        var (f2, s2) = _blackboard.TryGet<ulong>(_state2Key);
        if (!f1 || !f2)
            return false;

        var (rand, ns1, ns2) = RandomNumberGenerator.NextInt32(s1, s2);
        _blackboard.SetValue(_state1Key, ns1);
        _blackboard.SetValue(_state2Key, ns2);
        value = rand;
        return true;
    }

    /// <summary>Returns a random integer in <c>[minInclusive, maxExclusive)</c>; throws when state is not initialized.</summary>
    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "maxExclusive must be greater than minInclusive.");

        // Rejection sampling removes modulo bias so every value in the range
        // is equally likely (the raw XorShift128+ output is a uniform
        // 32-bit value). Range arithmetic is done in long so spans up to the
        // full 2^32-1 range (e.g. int.MinValue..int.MaxValue) do not overflow.
        var range = (long)maxExclusive - (long)minInclusive;
        var limit = ((1L << 32) - range) % range;
        while (true)
        {
            if (!TryNextInt32(out var raw))
                throw new InvalidOperationException("Random state not initialized. Call InitSeed first.");
            var r = (uint)raw;
            if (r >= limit)
                return (int)((r % range) + (long)minInclusive);
        }
    }

    /// <summary>Returns a random float in <c>[0, 1)</c>; throws when state is not initialized.</summary>
    public float NextFloat()
    {
        if (!TryNextInt32(out var raw))
            throw new InvalidOperationException("Random state not initialized. Call InitSeed first.");
        // Division by 2^32 keeps the raw value in [0, 1) as a double, but the float
        // conversion of raw values in [2^32 - 2^7, 2^32) rounds up to exactly 1.0f;
        // clamp that edge so the documented [0, 1) range holds.
        var result = (float)((uint)raw / 4294967296.0);
        return result >= 1.0f ? MathF.BitDecrement(1.0f) : result;
    }
}
