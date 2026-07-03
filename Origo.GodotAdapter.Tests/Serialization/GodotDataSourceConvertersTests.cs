using Godot;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.GodotAdapter.Serialization;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class GodotDataSourceConvertersTests
{
    private static DataSourceConverterRegistry CreateRegistry()
    {
        var mapping = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(mapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(registry);
        return registry;
    }

    [Fact]
    public void Vector2Converter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Vector2(1.5f, -2.5f);

        using var node = registry.Write(original);
        var restored = registry.Read<Vector2>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Vector2IConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Vector2I(3, -4);

        using var node = registry.Write(original);
        var restored = registry.Read<Vector2I>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Vector3IConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Vector3I(5, -6, 7);

        using var node = registry.Write(original);
        var restored = registry.Read<Vector3I>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Vector4Converter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Vector4(1.1f, 2.2f, 3.3f, 4.4f);

        using var node = registry.Write(original);
        var restored = registry.Read<Vector4>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void QuaternionConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f);

        using var node = registry.Write(original);
        var restored = registry.Read<Quaternion>(node);

        Assert.Equal(original.X, restored.X, 1e-6f);
        Assert.Equal(original.Y, restored.Y, 1e-6f);
        Assert.Equal(original.Z, restored.Z, 1e-6f);
        Assert.Equal(original.W, restored.W, 1e-6f);
    }

    [Fact]
    public void BasisConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Basis(
            new Vector3(1, 0, 0),
            new Vector3(0, 2, 0),
            new Vector3(0, 0, 3));

        using var node = registry.Write(original);
        var restored = registry.Read<Basis>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void BasisConverter_IdentityRoundTrip()
    {
        var registry = CreateRegistry();
        var original = Basis.Identity;

        using var node = registry.Write(original);
        var restored = registry.Read<Basis>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Transform2DConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Transform2D(
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(10, 20));

        using var node = registry.Write(original);
        var restored = registry.Read<Transform2D>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void ColorConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Color(0.1f, 0.2f, 0.3f, 0.4f);

        using var node = registry.Write(original);
        var restored = registry.Read<Color>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void ColorConverter_OpaqueWhiteRoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Color(1, 1, 1);

        using var node = registry.Write(original);
        var restored = registry.Read<Color>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Rect2Converter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Rect2(new Vector2(1, 2), new Vector2(3, 4));

        using var node = registry.Write(original);
        var restored = registry.Read<Rect2>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Rect2IConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Rect2I(new Vector2I(5, 6), new Vector2I(7, 8));

        using var node = registry.Write(original);
        var restored = registry.Read<Rect2I>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void AabbConverter_RoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Aabb(new Vector3(1, 2, 3), new Vector3(4, 5, 6));

        using var node = registry.Write(original);
        var restored = registry.Read<Aabb>(node);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void AabbConverter_ZeroSizeRoundTrip()
    {
        var registry = CreateRegistry();
        var original = new Aabb(new Vector3(0, 0, 0), new Vector3(0, 0, 0));

        using var node = registry.Write(original);
        var restored = registry.Read<Aabb>(node);

        Assert.Equal(original, restored);
    }
}
