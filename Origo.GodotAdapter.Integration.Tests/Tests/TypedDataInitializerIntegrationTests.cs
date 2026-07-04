using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class TypedDataInitializerIntegrationTests
{
    [IntegrationTest(Description = "IsLoaded always returns true and triggers assembly load")]
    public void IsLoaded_ReturnsTrue() => IntegrationTestRunner.Assert(TypedDataInitializer.IsLoaded, "IsLoaded should always be true.");
}
