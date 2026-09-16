using System;
using Origo.GodotAdapter.Snd;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class GodotPackedSceneNodeFactoryTests
{
    [Fact]
    public void Constructor_NullParent_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GodotPackedSceneNodeFactory(null!));
    }
}
