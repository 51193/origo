using System;
using Origo.Core.Snd.Metadata;

namespace Origo.TestSupport;

/// <summary>
///     Test-side reset for the process-wide TypedData kind registry. Kept in
///     the test support assembly so production code has no test-only hook.
/// </summary>
internal static class TypedDataTestSupport
{
    internal static void ResetKindRegistry()
    {
        Array.Clear(TypedData.KindTypeMap, 0, TypedData.KindTypeMap.Length);
        TypedDataLayeredRegistry.Reset();
        TypedDataHomeKindRegistration.Initialize();
    }
}
