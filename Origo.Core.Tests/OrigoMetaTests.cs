using Origo.Core;
using Xunit;

namespace Origo.Core.Tests;

public class OrigoMetaTests
{
    [Fact]
    public void DefaultBanner_IsNonEmpty() =>
        Assert.False(string.IsNullOrEmpty(OrigoMeta.DefaultBanner));

    [Fact]
    public void ToString_ContainsNameAndVersion()
    {
        var meta = new OrigoMeta("Origo", "1.0", "");
        var str = meta.ToString();

        Assert.Contains("Origo", str);
        Assert.Contains("1.0", str);
    }

    [Fact]
    public void EqualOperator_SameValues_ReturnsTrue()
    {
        var a = new OrigoMeta("Origo", "1.0", "banner");
        var b = new OrigoMeta("Origo", "1.0", "banner");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void EqualOperator_DifferentValues_ReturnsFalse()
    {
        var a = new OrigoMeta("Origo", "1.0", "a");
        var b = new OrigoMeta("Origo", "2.0", "a");

        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }
}
