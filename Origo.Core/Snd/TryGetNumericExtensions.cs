using System;
using Origo.Core.Abstractions.Entity;

namespace Origo.Core.Snd;

/// <summary>
///     Extension methods for numeric type coercion when reading entity data.
///     <c>SetData("key", 5)</c> stores an <c>int</c>, but a strategy may need
///     <c>float</c>. These helpers bridge the gap without manual type dispatch.
/// </summary>
public static class TryGetNumericExtensions
{
    public static bool TryGetNumeric(this ISndDataAccess access, string key, out float value)
    {
        var (foundFloat, f) = access.TryGetData<float>(key);
        if (foundFloat)
        {
            value = f;
            return true;
        }

        var (foundInt, i) = access.TryGetData<int>(key);
        if (foundInt)
        {
            value = i;
            return true;
        }

        var (foundLong, l) = access.TryGetData<long>(key);
        if (foundLong)
        {
            value = l;
            return true;
        }

        var (foundDouble, d) = access.TryGetData<double>(key);
        if (foundDouble)
        {
            value = (float)d;
            return true;
        }

        value = 0f;
        return false;
    }

    public static float GetNumeric(this ISndDataAccess access, string key, float fallback = 0f) => TryGetNumeric(access, key, out var value) ? value : fallback;
}
