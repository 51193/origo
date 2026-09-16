using System;

namespace Origo.Core.Random;

/// <summary>
///     Reproducible random number generator based on XorShift128+, with random state
///     explicitly maintained by the caller.
/// </summary>
public static class RandomNumberGenerator
{
    private const ulong _fnvOffsetBasis = 0xcbf29ce484222325;
    private const ulong _fnvPrime = 0x100000001b3;
    private const ulong _defaultStateValue = 0xBAD5EED;

    /// <summary>
    ///     Generates a reproducible initial random state from a string seed.
    /// </summary>
    public static (ulong s0, ulong s1) CreateStateFromSeed(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var hash1 = GetStableHash64(seed + "K6205");
        var hash2 = GetStableHash64(seed + "AMADEUS");

        return (hash1 == 0 ? _defaultStateValue : hash1, hash2 == 0 ? _defaultStateValue : hash2);
    }

    /// <summary>
    ///     Computes the next UInt64 from the given state and returns the next state.
    /// </summary>
    public static (ulong value, ulong nextS0, ulong nextS1) NextUInt64(ulong s0, ulong s1)
    {
        // Canonical xorshift128+ transition:
        //   x = s0; y = s1
        //   newS0 = y
        //   x ^= x << 23; x ^= x >> 17; x ^= y; x ^= y >> 26
        //   newS1 = x; output = newS0 + newS1
        var x = s0;
        var y = s1;
        var nextS0 = y;

        x ^= x << 23;
        x ^= x >> 17;
        x ^= y;
        x ^= y >> 26;

        return (nextS0 + x, nextS0, x);
    }

    /// <summary>Advances the generator and produces a signed 64-bit value.</summary>
    public static (long value, ulong nextS0, ulong nextS1) NextInt64(ulong s0, ulong s1)
    {
        var (value, nextS0, nextS1) = NextUInt64(s0, s1);
        return ((long)value, nextS0, nextS1);
    }

    /// <summary>Advances the generator and produces a signed 32-bit value.</summary>
    public static (int value, ulong nextS0, ulong nextS1) NextInt32(ulong s0, ulong s1)
    {
        var (value, nextS0, nextS1) = NextUInt64(s0, s1);
        return ((int)(value & 0xFFFFFFFF), nextS0, nextS1);
    }

    private static ulong GetStableHash64(string str)
    {
        var hash = _fnvOffsetBasis;

        foreach (var c in str)
        {
            hash ^= c;
            hash *= _fnvPrime;
        }

        return hash;
    }
}
