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
    /// <summary>
    ///     Reads a numeric value under the given key, trying float → int →
    ///     byte → sbyte → short → ushort → char → uint → ulong → long →
    ///     double in order. Returns false when the value is missing or not
    ///     numeric. As with all numeric coercion in this framework, precision
    ///     is not validated: wide integers convert to <c>float</c> with
    ///     possible precision loss (see <c>docs/Origo.Core/Snd/README</c>).
    /// </summary>
    public static bool TryGetNumeric(this ISndDataAccess access, string key, out float value)
    {
        ArgumentNullException.ThrowIfNull(access);
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

        if (access.TryGetData<byte>(key, out byte b))
        {
            value = b;
            return true;
        }

        if (access.TryGetData<sbyte>(key, out sbyte sb))
        {
            value = sb;
            return true;
        }

        if (access.TryGetData<short>(key, out short s))
        {
            value = s;
            return true;
        }

        if (access.TryGetData<ushort>(key, out ushort us))
        {
            value = us;
            return true;
        }

        if (access.TryGetData<char>(key, out char c))
        {
            value = c;
            return true;
        }

        if (access.TryGetData<uint>(key, out uint ui))
        {
            value = ui;
            return true;
        }

        if (access.TryGetData<ulong>(key, out ulong ul))
        {
            value = ul;
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

    /// <summary>Reads a numeric value under the given key, returning <paramref name="fallback" /> when absent.</summary>
    public static float GetNumeric(this ISndDataAccess access, string key, float fallback) => TryGetNumeric(access, key, out var value) ? value : fallback;
}
