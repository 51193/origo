using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class TypedDataInitializerIntegrationTests
{
    [IntegrationTest(Description = "EnsureLoaded triggers adapter assembly kind registration")]
    public void EnsureLoaded_TriggersAdapterKindRegistration()
    {
        TypedDataInitializer.EnsureLoaded();
        IntegrationTestRunner.AssertEqual((byte)128, TypedDataTypeMap.GetKindForType(typeof(Godot.Vector2)),
            "Adapter kind 128 should be registered after the assembly loads.");
    }
}
