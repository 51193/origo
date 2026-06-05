using System;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Provides extensibility hooks for adapter layers to contribute
///     their own typed kind mappings, object conversion, and factory logic.
///     Adapter assemblies register callbacks via <c>[ModuleInitializer]</c>
///     by calling the <c>Register*</c> methods.
/// </summary>
internal static class TypedDataLayeredRegistry
{
    private static Func<Type, byte>? _kindResolverChain;

    private static Func<byte, object, (long inlineBits, object? refValue)?>? _fromObjectChain;

    private static Func<TypedData, object?>? _toObjectChain;

    internal static void RegisterKindResolver(Func<Type, byte> resolver)
    {
        _kindResolverChain = _kindResolverChain is null
            ? resolver
            : (Func<Type, byte>)Delegate.Combine(_kindResolverChain, resolver);
    }

    internal static void RegisterFromObjectFallback(
        Func<byte, object, (long inlineBits, object? refValue)?> fallback)
    {
        _fromObjectChain = _fromObjectChain is null
            ? fallback
            : (Func<byte, object, (long inlineBits, object? refValue)?>)Delegate.Combine(
                _fromObjectChain, fallback);
    }

    internal static void RegisterToObjectFallback(Func<TypedData, object?> fallback)
    {
        _toObjectChain = _toObjectChain is null
            ? fallback
            : (Func<TypedData, object?>)Delegate.Combine(_toObjectChain, fallback);
    }

    internal static void Reset()
    {
        _kindResolverChain = null;
        _fromObjectChain = null;
        _toObjectChain = null;
    }

    internal static byte ResolveKind(Type type)
    {
        if (_kindResolverChain is null) return 0;
        foreach (Func<Type, byte> handler in _kindResolverChain.GetInvocationList())
        {
            var kind = handler(type);
            if (kind != 0) return kind;
        }
        return 0;
    }

    internal static (long inlineBits, object? refValue)? ResolveFromObject(byte kind, object value)
    {
        if (_fromObjectChain is null) return null;
        foreach (var handler in _fromObjectChain.GetInvocationList())
        {
            var result = ((Func<byte, object, (long inlineBits, object? refValue)?>)handler)(kind, value);
            if (result.HasValue) return result;
        }
        return null;
    }

    internal static object? ResolveToObject(TypedData td)
    {
        if (_toObjectChain is null) return null;
        foreach (var handler in _toObjectChain.GetInvocationList())
        {
            var result = ((Func<TypedData, object?>)handler)(td);
            if (result is not null) return result;
        }
        return null;
    }
}
