using System;
using System.Linq;
using System.Threading;

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

    // Cached invocation lists. Resolving a value is a hot read path and
    // Delegate.GetInvocationList() allocates a new array on every call.
    private static Func<Type, byte>[]? _kindResolverHandlers;

    private static Func<byte, object, (long inlineBits, object? refValue)?>[]? _fromObjectHandlers;

    private static Func<TypedData, object?>[]? _toObjectHandlers;

    private static readonly Lock _registrationGate = new();

    internal static void RegisterKindResolver(Func<Type, byte> resolver)
    {
        lock (_registrationGate)
        {
            _kindResolverChain = _kindResolverChain is null
                ? resolver
                : (Func<Type, byte>)Delegate.Combine(_kindResolverChain, resolver);
            Volatile.Write(ref _kindResolverHandlers,
                [.. _kindResolverChain.GetInvocationList().Cast<Func<Type, byte>>()]);
        }
    }

    internal static void RegisterFromObjectFallback(
        Func<byte, object, (long inlineBits, object? refValue)?> fallback)
    {
        lock (_registrationGate)
        {
            _fromObjectChain = _fromObjectChain is null
                ? fallback
                : (Func<byte, object, (long inlineBits, object? refValue)?>)Delegate.Combine(
                    _fromObjectChain, fallback);
            Volatile.Write(ref _fromObjectHandlers,
                [.. _fromObjectChain.GetInvocationList()
                    .Cast<Func<byte, object, (long inlineBits, object? refValue)?>>()]);
        }
    }

    internal static void RegisterToObjectFallback(Func<TypedData, object?> fallback)
    {
        lock (_registrationGate)
        {
            _toObjectChain = _toObjectChain is null
                ? fallback
                : (Func<TypedData, object?>)Delegate.Combine(_toObjectChain, fallback);
            Volatile.Write(ref _toObjectHandlers,
                [.. _toObjectChain.GetInvocationList().Cast<Func<TypedData, object?>>()]);
        }
    }

    internal static byte ResolveKind(Type type)
    {
        var handlers = Volatile.Read(ref _kindResolverHandlers);
        if (handlers is null) return 0;
        foreach (var handler in handlers)
        {
            var kind = handler(type);
            if (kind != 0) return kind;
        }
        return 0;
    }

    internal static (long inlineBits, object? refValue)? ResolveFromObject(byte kind, object value)
    {
        var handlers = Volatile.Read(ref _fromObjectHandlers);
        if (handlers is null) return null;
        foreach (var handler in handlers)
        {
            var result = handler(kind, value);
            if (result.HasValue) return result;
        }
        return null;
    }

    internal static object? ResolveToObject(TypedData td)
    {
        var handlers = Volatile.Read(ref _toObjectHandlers);
        if (handlers is null) return null;
        foreach (var handler in handlers)
        {
            var result = handler(td);
            if (result is not null) return result;
        }
        return null;
    }
}
