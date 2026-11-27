using System;
using System.Reflection;
using Origo.Core.Snd.Metadata;

namespace Origo.TestSupport;

/// <summary>
///     Test-side reset for the process-wide TypedData kind registry and the
///     layered adapter registration chains. The registry array is internal
///     and therefore directly writable via InternalsVisibleTo; the private
///     static chain and cached-handler fields are reset through reflection so
///     production code carries no test-only reset hook (see META-TEST §8/§9).
///     Call only while test parallelization is disabled; the reset is not
///     synchronized with a concurrent registration.
/// </summary>
internal static class TypedDataTestSupport
{
    private static readonly FieldInfo _kindResolverChainField =
        typeof(TypedDataLayeredRegistry).GetField(
            "_kindResolverChain", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "TypedDataLayeredRegistry._kindResolverChain not found; update the test-support reset.");

    private static readonly FieldInfo _fromObjectChainField =
        typeof(TypedDataLayeredRegistry).GetField(
            "_fromObjectChain", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "TypedDataLayeredRegistry._fromObjectChain not found; update the test-support reset.");

    private static readonly FieldInfo _toObjectChainField =
        typeof(TypedDataLayeredRegistry).GetField(
            "_toObjectChain", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "TypedDataLayeredRegistry._toObjectChain not found; update the test-support reset.");

    private static readonly FieldInfo _kindResolverHandlersField =
        typeof(TypedDataLayeredRegistry).GetField(
            "_kindResolverHandlers", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "TypedDataLayeredRegistry._kindResolverHandlers not found; update the test-support reset.");

    private static readonly FieldInfo _fromObjectHandlersField =
        typeof(TypedDataLayeredRegistry).GetField(
            "_fromObjectHandlers", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "TypedDataLayeredRegistry._fromObjectHandlers not found; update the test-support reset.");

    private static readonly FieldInfo _toObjectHandlersField =
        typeof(TypedDataLayeredRegistry).GetField(
            "_toObjectHandlers", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "TypedDataLayeredRegistry._toObjectHandlers not found; update the test-support reset.");

    internal static void ResetKindRegistry()
    {
        Array.Clear(TypedData.KindTypeMap, 0, TypedData.KindTypeMap.Length);
        _kindResolverChainField.SetValue(null, null);
        _fromObjectChainField.SetValue(null, null);
        _toObjectChainField.SetValue(null, null);
        _kindResolverHandlersField.SetValue(null, null);
        _fromObjectHandlersField.SetValue(null, null);
        _toObjectHandlersField.SetValue(null, null);
        TypedDataHomeKindRegistration.Initialize();
    }
}
