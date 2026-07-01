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
    private const string DefaultState1Key = "rand.state1";
    private const string DefaultState2Key = "rand.state2";

    private readonly IBlackboard _blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
    private readonly string _state1Key = string.IsNullOrWhiteSpace(state1Key) ? DefaultState1Key : state1Key;
    private readonly string _state2Key = string.IsNullOrWhiteSpace(state2Key) ? DefaultState2Key : state2Key;

    public bool InitSeed(string seed)
    {
        var (s0, s1) = RandomNumberGenerator.CreateStateFromSeed(seed);
        _blackboard.SetValue(_state1Key, s0);
        _blackboard.SetValue(_state2Key, s1);
        return true;
    }

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

    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "maxExclusive must be greater than minInclusive.");
        if (!TryNextInt32(out var raw))
            throw new InvalidOperationException("Random state not initialized. Call InitSeed first.");
        return (int)(((long)(uint)raw % (long)(maxExclusive - minInclusive)) + minInclusive);
    }

    public float NextFloat()
    {
        if (!TryNextInt32(out var raw))
            throw new InvalidOperationException("Random state not initialized. Call InitSeed first.");
        return (float)((uint)raw / (double)uint.MaxValue);
    }
}
