using System.Runtime.CompilerServices;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotAdapterTypedDataRegistrationIntegrationTests
{
    [IntegrationTest(Description = "GodotAdapter assembly module initializers register TypedData kinds")]
    public void GodotAdapterAssemblyLoad_RegistersTypedDataKinds()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(GodotSndManager).Module.ModuleHandle);
        IntegrationTestRunner.AssertEqual((byte)128, TypedDataTypeMap.GetKindForType(typeof(Godot.Vector2)),
            "Adapter kind 128 should be registered after the assembly loads.");
    }
}
