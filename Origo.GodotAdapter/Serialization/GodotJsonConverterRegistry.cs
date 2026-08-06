using System;
using Godot;
using Origo.Core.DataSource;
using Origo.Core.Serialization;

namespace Origo.GodotAdapter.Serialization;

/// <summary>
///     One-stop registration of all Godot built-in type <see cref="TypeStringMapping" /> mappings and JsonConverters.
/// </summary>
public static class GodotJsonConverterRegistry
{
    /// <summary>Registers all Godot built-in type-name mappings (Vector2, Vector3, Color, ...).</summary>
    public static void RegisterTypeMappings(TypeStringMapping typeMapping)
    {
        ArgumentNullException.ThrowIfNull(typeMapping);
        typeMapping.RegisterType<Vector2>(GodotEngineTypeNames.Vector2);
        typeMapping.RegisterType<Vector2I>(GodotEngineTypeNames.Vector2I);
        typeMapping.RegisterType<Vector3>(GodotEngineTypeNames.Vector3);
        typeMapping.RegisterType<Vector3I>(GodotEngineTypeNames.Vector3I);
        typeMapping.RegisterType<Vector4>(GodotEngineTypeNames.Vector4);
        typeMapping.RegisterType<Quaternion>(GodotEngineTypeNames.Quaternion);
        typeMapping.RegisterType<Basis>(GodotEngineTypeNames.Basis);
        typeMapping.RegisterType<Transform2D>(GodotEngineTypeNames.Transform2D);
        typeMapping.RegisterType<Transform3D>(GodotEngineTypeNames.Transform3D);
        typeMapping.RegisterType<Color>(GodotEngineTypeNames.Color);
        typeMapping.RegisterType<Rect2>(GodotEngineTypeNames.Rect2);
        typeMapping.RegisterType<Rect2I>(GodotEngineTypeNames.Rect2I);
        typeMapping.RegisterType<Aabb>(GodotEngineTypeNames.Aabb);
        typeMapping.RegisterType<Plane>(GodotEngineTypeNames.Plane);
    }

    /// <summary>Registers data-source converters for all Godot built-in types.</summary>
    public static void RegisterDataSourceConverters(DataSourceConverterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new Vector2DataSourceConverter());
        registry.Register(new Vector2IDataSourceConverter());
        registry.Register(new Vector3DataSourceConverter());
        registry.Register(new Vector3IDataSourceConverter());
        registry.Register(new Vector4DataSourceConverter());
        registry.Register(new QuaternionDataSourceConverter());
        registry.Register(new BasisDataSourceConverter());
        registry.Register(new Transform2DDataSourceConverter());
        registry.Register(new Transform3DDataSourceConverter());
        registry.Register(new ColorDataSourceConverter());
        registry.Register(new Rect2DataSourceConverter());
        registry.Register(new Rect2IDataSourceConverter());
        registry.Register(new AabbDataSourceConverter());
        registry.Register(new PlaneDataSourceConverter());
    }
}
