using Origo.GodotAdapter.Snd;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class TypedDataInitializerTests
{
    [Fact]
    public void IsLoaded_ReturnsTrue() => Assert.True(TypedDataInitializer.IsLoaded);
}
