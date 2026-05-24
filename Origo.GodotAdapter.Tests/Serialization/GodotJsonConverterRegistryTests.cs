using Godot;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.GodotAdapter.Serialization;
using Xunit;

namespace Origo.GodotAdapter.Tests.SerializationTests;

public class GodotJsonConverterRegistryTests
{
    [Fact]
    public void RegisterTypeMappings_RegistersExpectedTypeNames()
    {
        var mapping = new TypeStringMapping();

        GodotJsonConverterRegistry.RegisterTypeMappings(mapping);

        Assert.Equal("Vector2", mapping.GetNameByType(typeof(Vector2)));
        Assert.Equal("Vector3", mapping.GetNameByType(typeof(Vector3)));
        Assert.Equal("Transform3D", mapping.GetNameByType(typeof(Transform3D)));
        Assert.Equal(typeof(Color), mapping.GetTypeByName("Color"));
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
}
