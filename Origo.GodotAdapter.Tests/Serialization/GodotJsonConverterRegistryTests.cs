using Godot;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.GodotAdapter.Serialization;
using Xunit;

namespace Origo.GodotAdapter.Tests.SerializationTests;

public class GodotJsonConverterRegistryTests
{
    [Fact]
    public void RegisterTypeMappings_RegistersAll14TypeNames()
    {
        var mapping = new TypeStringMapping();

        GodotJsonConverterRegistry.RegisterTypeMappings(mapping);

        Assert.Equal("Vector2", mapping.GetNameByType(typeof(Vector2)));
        Assert.Equal("Vector2I", mapping.GetNameByType(typeof(Vector2I)));
        Assert.Equal("Vector3", mapping.GetNameByType(typeof(Vector3)));
        Assert.Equal("Vector3I", mapping.GetNameByType(typeof(Vector3I)));
        Assert.Equal("Vector4", mapping.GetNameByType(typeof(Vector4)));
        Assert.Equal("Quaternion", mapping.GetNameByType(typeof(Quaternion)));
        Assert.Equal("Basis", mapping.GetNameByType(typeof(Basis)));
        Assert.Equal("Transform2D", mapping.GetNameByType(typeof(Transform2D)));
        Assert.Equal("Transform3D", mapping.GetNameByType(typeof(Transform3D)));
        Assert.Equal("Color", mapping.GetNameByType(typeof(Color)));
        Assert.Equal("Rect2", mapping.GetNameByType(typeof(Rect2)));
        Assert.Equal("Rect2I", mapping.GetNameByType(typeof(Rect2I)));
        Assert.Equal("Aabb", mapping.GetNameByType(typeof(Aabb)));
        Assert.Equal("Plane", mapping.GetNameByType(typeof(Plane)));
    }

    [Fact]
    public void RegisterTypeMappings_AllTypesCanBeResolvedByName()
    {
        var mapping = new TypeStringMapping();
        GodotJsonConverterRegistry.RegisterTypeMappings(mapping);

        Assert.Equal(typeof(Vector2), mapping.GetTypeByName("Vector2"));
        Assert.Equal(typeof(Vector2I), mapping.GetTypeByName("Vector2I"));
        Assert.Equal(typeof(Vector3), mapping.GetTypeByName("Vector3"));
        Assert.Equal(typeof(Vector3I), mapping.GetTypeByName("Vector3I"));
        Assert.Equal(typeof(Vector4), mapping.GetTypeByName("Vector4"));
        Assert.Equal(typeof(Quaternion), mapping.GetTypeByName("Quaternion"));
        Assert.Equal(typeof(Basis), mapping.GetTypeByName("Basis"));
        Assert.Equal(typeof(Transform2D), mapping.GetTypeByName("Transform2D"));
        Assert.Equal(typeof(Transform3D), mapping.GetTypeByName("Transform3D"));
        Assert.Equal(typeof(Color), mapping.GetTypeByName("Color"));
        Assert.Equal(typeof(Rect2), mapping.GetTypeByName("Rect2"));
        Assert.Equal(typeof(Rect2I), mapping.GetTypeByName("Rect2I"));
        Assert.Equal(typeof(Aabb), mapping.GetTypeByName("Aabb"));
        Assert.Equal(typeof(Plane), mapping.GetTypeByName("Plane"));
    }

    [Fact]
    public void RegisterDataSourceConverters_AllowsVectorRoundTrip()
    {
        var mapping = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(mapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(registry);

        using var node = registry.Write(new Vector3(1.5f, 2.5f, 3.5f));
        var value = registry.Read<Vector3>(node);

        Assert.Equal(new Vector3(1.5f, 2.5f, 3.5f), value);
    }

    [Fact]
    public void RegisterDataSourceConverters_AllowsTransformAndPlaneConverters()
    {
        var mapping = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(mapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(registry);

        var transform = new Transform3D(Basis.Identity, new Vector3(2, 3, 4));
        using var transformNode = registry.Write(transform);
        var restoredTransform = registry.Read<Transform3D>(transformNode);
        Assert.Equal(transform, restoredTransform);

        var plane = new Plane(new Vector3(0, 1, 0), 7);
        using var planeNode = registry.Write(plane);
        var restoredPlane = registry.Read<Plane>(planeNode);
        Assert.Equal(plane, restoredPlane);
    }

    [Fact]
    public void RegisterDataSourceConverters_Vector2IAnd3IRoundTrip()
    {
        var mapping = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(mapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(registry);

        using var v2Node = registry.Write(new Vector2I(-3, 7));
        Assert.Equal(new Vector2I(-3, 7), registry.Read<Vector2I>(v2Node));

        using var v3Node = registry.Write(new Vector3I(1, -2, 3));
        Assert.Equal(new Vector3I(1, -2, 3), registry.Read<Vector3I>(v3Node));
    }

    [Fact]
    public void RegisterDataSourceConverters_Vector4AndQuaternionRoundTrip()
    {
        var mapping = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(mapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(registry);

        using var v4Node = registry.Write(new Vector4(0.5f, 1.5f, 2.5f, 3.5f));
        Assert.Equal(new Vector4(0.5f, 1.5f, 2.5f, 3.5f), registry.Read<Vector4>(v4Node));

        using var qNode = registry.Write(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f));
        var q = registry.Read<Quaternion>(qNode);
        Assert.Equal(0.1f, q.X, 1e-6f);
        Assert.Equal(0.2f, q.Y, 1e-6f);
        Assert.Equal(0.3f, q.Z, 1e-6f);
        Assert.Equal(0.9f, q.W, 1e-6f);
    }

    [Fact]
    public void RegisterDataSourceConverters_Rect2AndRect2IRoundTrip()
    {
        var mapping = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(mapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(registry);

        using var r2Node = registry.Write(new Rect2(new Vector2(1, 2), new Vector2(30, 40)));
        Assert.Equal(new Rect2(new Vector2(1, 2), new Vector2(30, 40)), registry.Read<Rect2>(r2Node));

        using var r2iNode = registry.Write(new Rect2I(new Vector2I(5, 6), new Vector2I(7, 8)));
        Assert.Equal(new Rect2I(new Vector2I(5, 6), new Vector2I(7, 8)), registry.Read<Rect2I>(r2iNode));
    }

    [Fact]
    public void RegisterDataSourceConverters_AabbRoundTrip()
    {
        var mapping = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(mapping);
        GodotJsonConverterRegistry.RegisterDataSourceConverters(registry);

        using var node = registry.Write(new Aabb(new Vector3(1, 2, 3), new Vector3(10, 20, 30)));
        Assert.Equal(new Aabb(new Vector3(1, 2, 3), new Vector3(10, 20, 30)), registry.Read<Aabb>(node));
    }
}
