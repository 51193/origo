using Origo.Core.Snd.Scene;
using Xunit;

namespace Origo.Core.Tests;

public class NullNodeFactoryTests
{
    [Fact]
    public void NullNodeFactory_CreatesNullNodeHandle()
    {
        var factory = new NullNodeFactory();
        var handle = factory.Create("test_node", "res://test.tscn");

        Assert.Equal("test_node", handle.Name);
        Assert.NotNull(handle.Native);

        // Free and SetVisible are no-ops.
        handle.Free();
        handle.SetVisible(false);
        handle.SetVisible(true);
    }
}
