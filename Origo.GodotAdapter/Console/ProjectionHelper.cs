using Godot;

namespace Origo.GodotAdapter.Console;

/// <summary>
///     Projects a world-space position to screen-space coordinates
///     using a Godot Camera3D transform and projection matrix.
/// </summary>
internal static class ProjectionHelper
{
    public static Vector3? ProjectWorldToScreen(
        Transform3D cameraGlobal,
        Projection cameraProj,
        Vector3 worldPos,
        Vector2 viewportSize)
    {
        Vector3 localPos = cameraGlobal.AffineInverse() * worldPos;

        if (localPos.Z >= 0f)
            return null;

        Vector4 clip = cameraProj * new Vector4(localPos.X, localPos.Y, localPos.Z, 1.0f);

        if (Mathf.IsZeroApprox(clip.W))
            return null;

        var ndcX = clip.X / clip.W;
        var ndcY = clip.Y / clip.W;
        var ndcZ = clip.Z / clip.W;

        if (ndcX < -1f || ndcX > 1f || ndcY < -1f || ndcY > 1f || ndcZ < 0f || ndcZ > 1f)
            return null;

        var screenX = (ndcX * 0.5f + 0.5f) * viewportSize.X;
        var screenY = (0.5f - ndcY * 0.5f) * viewportSize.Y;

        return new Vector3(screenX, screenY, -localPos.Z);
    }
}
