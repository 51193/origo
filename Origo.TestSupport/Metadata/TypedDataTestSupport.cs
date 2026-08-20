using System;
using System.Reflection;
using Origo.Core.Snd.Metadata;

namespace Origo.TestSupport;

/// <summary>
///     Test-side reset for the process-wide TypedData kind registry and the
///     layered adapter registration chains. The registry array is internal
///     and therefore directly writable via InternalsVisibleTo; the private
///     static chain fields are reset through reflection so production code
///     carries no test-only reset hook (see META-TEST §8/§9).
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

    internal static void ResetKindRegistry()
    {
        Array.Clear(TypedData.KindTypeMap, 0, TypedData.KindTypeMap.Length);
        _kindResolverChainField.SetValue(null, null);
        _fromObjectChainField.SetValue(null, null);
        _toObjectChainField.SetValue(null, null);
        TypedDataHomeKindRegistration.Initialize();
    }
}
