using Godot;
using Origo.GodotAdapter.Console;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class ProjectionHelperTests
{
    private static readonly Transform3D _identityCamera = Transform3D.Identity;

    private static Projection CreatePerspective90() => Projection.CreatePerspective(90f, 1.0f, 0.1f, 100f, false);

    private static readonly Vector2 _viewport800x600 = new(800f, 600f);

    [Fact]
    public void ProjectWorldToScreen_Center_ReturnsScreenCenter()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, -5f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(400f, result!.Value.X, 0.5f);
        Assert.Equal(300f, result.Value.Y, 0.5f);
        Assert.Equal(5f, result.Value.Z, 0.01f);
    }

    [Fact]
    public void ProjectWorldToScreen_RightEdge_ReturnsRightBoundary()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(5f, 0f, -5f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(800f, result!.Value.X, 0.5f);
        Assert.Equal(300f, result.Value.Y, 0.5f);
    }

    [Fact]
    public void ProjectWorldToScreen_LeftEdge_ReturnsLeftBoundary()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(-5f, 0f, -5f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(0f, result!.Value.X, 0.5f);
        Assert.Equal(300f, result.Value.Y, 0.5f);
    }

    [Fact]
    public void ProjectWorldToScreen_TopEdge_ReturnsTopBoundary()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 5f, -5f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(400f, result!.Value.X, 0.5f);
        Assert.Equal(0f, result.Value.Y, 0.5f);
    }

    [Fact]
    public void ProjectWorldToScreen_BottomEdge_ReturnsBottomBoundary()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, -5f, -5f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(400f, result!.Value.X, 0.5f);
        Assert.Equal(600f, result.Value.Y, 0.5f);
    }

    [Fact]
    public void ProjectWorldToScreen_NearPlane_ReturnsVisiblePoint()
    {
        // Godot's CPU camera projection maps near to NDC Z -1 and far to +1.
        // A point between the near plane and the far plane can therefore have
        // negative NDC Z; it must not be discarded as behind the camera.
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, -0.15f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(400f, result!.Value.X, 0.5f);
        Assert.Equal(300f, result.Value.Y, 0.5f);
        Assert.Equal(0.15f, result.Value.Z, 0.01f);
    }

    [Fact]
    public void ProjectWorldToScreen_OrthogonalInterior_ReturnsVisiblePoint()
    {
        var proj = Projection.CreateOrthogonal(-4f, 4f, -3f, 3f, 0.1f, 100f);
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, -5f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(400f, result!.Value.X, 0.5f);
        Assert.Equal(300f, result.Value.Y, 0.5f);
        Assert.Equal(5f, result.Value.Z, 0.01f);
    }

    [Fact]
    public void ProjectWorldToScreen_PerspectiveBeyondFarPlane_ReturnsNull()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, -101f), _viewport800x600);

        Assert.Null(result);
    }

    [Fact]
    public void ProjectWorldToScreen_OrthogonalBeyondFarPlane_ReturnsNull()
    {
        var proj = Projection.CreateOrthogonal(-4f, 4f, -3f, 3f, 0.1f, 100f);
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, -101f), _viewport800x600);

        Assert.Null(result);
    }

    [Fact]
    public void ProjectWorldToScreen_BehindCamera_ReturnsNull()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, 5f), _viewport800x600);

        Assert.Null(result);
    }

    [Fact]
    public void ProjectWorldToScreen_OutsideFrustum_ReturnsNull()
    {
        var proj = CreatePerspective90();
        var result = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(100f, 0f, -5f), _viewport800x600);

        Assert.Null(result);
    }

    [Fact]
    public void ProjectWorldToScreen_DepthIncreasesWithDistance()
    {
        var proj = CreatePerspective90();
        var near = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, -2f), _viewport800x600);
        var far = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(0f, 0f, -10f), _viewport800x600);

        Assert.NotNull(near);
        Assert.NotNull(far);
        Assert.True(far!.Value.Z > near!.Value.Z);
    }

    [Fact]
    public void ProjectWorldToScreen_SymmetricPositions_HaveSymmetricScreenX()
    {
        var proj = CreatePerspective90();
        var left = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(-3f, 0f, -5f), _viewport800x600);
        var right = ProjectionHelper.ProjectWorldToScreen(
            _identityCamera, proj, new Vector3(3f, 0f, -5f), _viewport800x600);

        Assert.NotNull(left);
        Assert.NotNull(right);
        var centerX = 400f;
        Assert.True(right!.Value.X - centerX > 0);
        Assert.True(centerX - left!.Value.X > 0);
        Assert.Equal(right.Value.X - centerX, centerX - left.Value.X, 0.1f);
    }

    [Fact]
    public void ProjectWorldToScreen_CameraNotAtOrigin_ProjectsCorrectly()
    {
        var proj = CreatePerspective90();
        var cameraAt = new Transform3D(Basis.Identity, new Vector3(0f, 0f, 10f));
        var result = ProjectionHelper.ProjectWorldToScreen(
            cameraAt, proj, new Vector3(0f, 0f, 5f), _viewport800x600);

        Assert.NotNull(result);
        Assert.Equal(400f, result!.Value.X, 0.5f);
        Assert.Equal(300f, result.Value.Y, 0.5f);
        Assert.Equal(5f, result.Value.Z, 0.01f);
    }
}
