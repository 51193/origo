using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Snd;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class TypedDataInitializerTests
{
    [Fact]
    public void EnsureLoaded_TriggersAdapterKindRegistration()
    {
        TypedDataInitializer.EnsureLoaded();
        Assert.Equal((byte)128, TypedDataTypeMap.GetKindForType(typeof(Godot.Vector2)));
    }
}
