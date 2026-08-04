using Godot;
using Origo.Core.DataSource;

namespace Origo.GodotAdapter.Serialization;

/// <summary>
///     Bidirectional converters for Godot engine types
///     (Vector2, Vector2I, Vector3, Vector3I, Vector4, Quaternion, Basis,
///     Transform2D, Transform3D, Color, Rect2, Rect2I, Aabb, Plane)
///     via <see cref="DataSourceNode" />.
/// </summary>

internal sealed class Vector2DataSourceConverter : DataSourceConverter<Vector2>
{
    public override Vector2 Read(DataSourceNode node) => new(node["x"].As<float>(), node["y"].As<float>());

    public override DataSourceNode Write(Vector2 value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateNumber(value.X))
            .Add("y", DataSourceNode.CreateNumber(value.Y));
    }
}

internal sealed class Vector2IDataSourceConverter : DataSourceConverter<Vector2I>
{
    public override Vector2I Read(DataSourceNode node) => new(node["x"].As<int>(), node["y"].As<int>());

    public override DataSourceNode Write(Vector2I value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateNumber(value.X))
            .Add("y", DataSourceNode.CreateNumber(value.Y));
    }
}

internal sealed class Vector3DataSourceConverter : DataSourceConverter<Vector3>
{
    public override Vector3 Read(DataSourceNode node) =>
        new(node["x"].As<float>(), node["y"].As<float>(), node["z"].As<float>());

    public override DataSourceNode Write(Vector3 value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateNumber(value.X))
            .Add("y", DataSourceNode.CreateNumber(value.Y))
            .Add("z", DataSourceNode.CreateNumber(value.Z));
    }
}

internal sealed class Vector3IDataSourceConverter : DataSourceConverter<Vector3I>
{
    public override Vector3I Read(DataSourceNode node) => new(node["x"].As<int>(), node["y"].As<int>(), node["z"].As<int>());

    public override DataSourceNode Write(Vector3I value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateNumber(value.X))
            .Add("y", DataSourceNode.CreateNumber(value.Y))
            .Add("z", DataSourceNode.CreateNumber(value.Z));
    }
}

internal sealed class Vector4DataSourceConverter : DataSourceConverter<Vector4>
{
    public override Vector4 Read(DataSourceNode node) => new(node["x"].As<float>(), node["y"].As<float>(),
        node["z"].As<float>(), node["w"].As<float>());

    public override DataSourceNode Write(Vector4 value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateNumber(value.X))
            .Add("y", DataSourceNode.CreateNumber(value.Y))
            .Add("z", DataSourceNode.CreateNumber(value.Z))
            .Add("w", DataSourceNode.CreateNumber(value.W));
    }
}

internal sealed class QuaternionDataSourceConverter : DataSourceConverter<Quaternion>
{
    public override Quaternion Read(DataSourceNode node) => new(node["x"].As<float>(), node["y"].As<float>(),
        node["z"].As<float>(), node["w"].As<float>());

    public override DataSourceNode Write(Quaternion value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateNumber(value.X))
            .Add("y", DataSourceNode.CreateNumber(value.Y))
            .Add("z", DataSourceNode.CreateNumber(value.Z))
            .Add("w", DataSourceNode.CreateNumber(value.W));
    }
}

internal sealed class ColorDataSourceConverter : DataSourceConverter<Color>
{
    public override Color Read(DataSourceNode node) => new(node["r"].As<float>(), node["g"].As<float>(),
        node["b"].As<float>(), node["a"].As<float>());

    public override DataSourceNode Write(Color value)
    {
        return DataSourceNode.CreateObject()
            .Add("r", DataSourceNode.CreateNumber(value.R))
            .Add("g", DataSourceNode.CreateNumber(value.G))
            .Add("b", DataSourceNode.CreateNumber(value.B))
            .Add("a", DataSourceNode.CreateNumber(value.A));
    }
}

internal sealed class BasisDataSourceConverter : DataSourceConverter<Basis>
{
    private readonly Vector3DataSourceConverter _vec3 = new();

    public override Basis Read(DataSourceNode node) =>
        new(_vec3.Read(node["x"]), _vec3.Read(node["y"]), _vec3.Read(node["z"]));

    public override DataSourceNode Write(Basis value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", _vec3.Write(value.X))
            .Add("y", _vec3.Write(value.Y))
            .Add("z", _vec3.Write(value.Z));
    }
}

internal sealed class Transform2DDataSourceConverter : DataSourceConverter<Transform2D>
{
    private readonly Vector2DataSourceConverter _vec2 = new();

    public override Transform2D Read(DataSourceNode node) =>
        new(_vec2.Read(node["x"]), _vec2.Read(node["y"]), _vec2.Read(node["origin"]));

    public override DataSourceNode Write(Transform2D value)
    {
        return DataSourceNode.CreateObject()
            .Add("x", _vec2.Write(value.X))
            .Add("y", _vec2.Write(value.Y))
            .Add("origin", _vec2.Write(value.Origin));
    }
}

internal sealed class Transform3DDataSourceConverter : DataSourceConverter<Transform3D>
{
    private readonly BasisDataSourceConverter _basis = new();
    private readonly Vector3DataSourceConverter _vec3 = new();

    public override Transform3D Read(DataSourceNode node) =>
        new(_basis.Read(node["basis"]), _vec3.Read(node["origin"]));

    public override DataSourceNode Write(Transform3D value)
    {
        return DataSourceNode.CreateObject()
            .Add("basis", _basis.Write(value.Basis))
            .Add("origin", _vec3.Write(value.Origin));
    }
}

internal sealed class Rect2DataSourceConverter : DataSourceConverter<Rect2>
{
    private readonly Vector2DataSourceConverter _vec2 = new();

    public override Rect2 Read(DataSourceNode node) => new(_vec2.Read(node["position"]), _vec2.Read(node["size"]));

    public override DataSourceNode Write(Rect2 value)
    {
        return DataSourceNode.CreateObject()
            .Add("position", _vec2.Write(value.Position))
            .Add("size", _vec2.Write(value.Size));
    }
}

internal sealed class Rect2IDataSourceConverter : DataSourceConverter<Rect2I>
{
    private readonly Vector2IDataSourceConverter _vec2I = new();

    public override Rect2I Read(DataSourceNode node) => new(_vec2I.Read(node["position"]), _vec2I.Read(node["size"]));

    public override DataSourceNode Write(Rect2I value)
    {
        return DataSourceNode.CreateObject()
            .Add("position", _vec2I.Write(value.Position))
            .Add("size", _vec2I.Write(value.Size));
    }
}

internal sealed class AabbDataSourceConverter : DataSourceConverter<Aabb>
{
    private readonly Vector3DataSourceConverter _vec3 = new();

    public override Aabb Read(DataSourceNode node) => new(_vec3.Read(node["position"]), _vec3.Read(node["size"]));

    public override DataSourceNode Write(Aabb value)
    {
        return DataSourceNode.CreateObject()
            .Add("position", _vec3.Write(value.Position))
            .Add("size", _vec3.Write(value.Size));
    }
}

internal sealed class PlaneDataSourceConverter : DataSourceConverter<Plane>
{
    private readonly Vector3DataSourceConverter _vec3 = new();

    public override Plane Read(DataSourceNode node) => new(_vec3.Read(node["normal"]), node["d"].As<float>());

    public override DataSourceNode Write(Plane value)
    {
        return DataSourceNode.CreateObject()
            .Add("normal", _vec3.Write(value.Normal))
            .Add("d", DataSourceNode.CreateNumber(value.D));
    }
}
