using System;

namespace Origo.Core.Random;

/// <summary>
///     基于 XorShift128+ 的可复现随机数实现，随机状态由调用方显式维护。
/// </summary>
public static class RandomNumberGenerator
{
    private const ulong FnvOffsetBasis = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;
    private const ulong DefaultStateValue = 0xBAD5EED;

    /// <summary>
    ///     由字符串种子生成可复现的初始随机状态。
    /// </summary>
    public static (ulong s0, ulong s1) CreateStateFromSeed(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var hash1 = GetStableHash64(seed + "K6205");
        var hash2 = GetStableHash64(seed + "AMADEUS");

        return (hash1 == 0 ? DefaultStateValue : hash1, hash2 == 0 ? DefaultStateValue : hash2);
    }

    /// <summary>
    ///     从传入状态计算下一个 UInt64，并返回下一状态。
    /// </summary>
    public static (ulong value, ulong nextS0, ulong nextS1) NextUInt64(ulong s0, ulong s1)
    {
        var nextS1 = s0;
        var working = s1;

        working ^= working << 23;
        working ^= working >> 17;
        working ^= s0;
        working ^= s0 >> 26;

        var nextS2 = working;
        return (nextS1 + nextS2, nextS1, nextS2);
    }

    public static (long value, ulong nextS0, ulong nextS1) NextInt64(ulong s0, ulong s1)
    {
        var (value, nextS0, nextS1) = NextUInt64(s0, s1);
        return ((long)value, nextS0, nextS1);
    }

    public static (int value, ulong nextS0, ulong nextS1) NextInt32(ulong s0, ulong s1)
    {
        var (value, nextS0, nextS1) = NextUInt64(s0, s1);
        return ((int)(value & 0xFFFFFFFF), nextS0, nextS1);
    }

    private static ulong GetStableHash64(string str)
    {
        var hash = FnvOffsetBasis;

        foreach (var c in str)
        {
            hash ^= c;
            hash *= FnvPrime;
        }

        return hash;
    }
}
