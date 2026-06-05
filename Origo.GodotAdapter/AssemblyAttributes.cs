using Godot;
using Origo.Core.Snd.Metadata;

[assembly: SndInlineTypes(
    startKind: 128,
    typeof(Vector2), typeof(Vector2I),
    typeof(Vector3), typeof(Vector3I), typeof(Vector4),
    typeof(Quaternion),
    typeof(Basis),
    typeof(Transform2D), typeof(Transform3D),
    typeof(Color),
    typeof(Rect2), typeof(Rect2I),
    typeof(Aabb),
    typeof(Plane)
)]
