using System.Globalization;
using System.Text;
using Godot;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Console;

/// <summary><c>camera_view</c> — show screen-space coordinates and depth of visible entity nodes.</summary>
internal sealed class CameraViewCommandHandler(IOrigoRuntime runtime) : CommandHandlerBase(runtime)
{
    public override string Name => "camera_view";

    public override string HelpText => "camera_view — show screen coordinates and depth of all visible entity nodes under the current camera.";

    public override int MinPositionalArgs => 0;

    public override int MaxPositionalArgs => 0;

    private struct NodeCount
    {
        public int Total;
        public int ThreeD;
        public int Ui;
    }

    protected override bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var mainLoop = Engine.GetMainLoop();
        if (mainLoop is not SceneTree sceneTree)
        {
            errorMessage = "Cannot access the scene tree (possibly not running inside a Godot engine).";
            return false;
        }

        var viewport = sceneTree.Root;
        var camera = viewport.GetCamera3D();
        if (camera is null)
        {
            errorMessage = "The current viewport has no active Camera3D.";
            return false;
        }

        var session = Runtime.SessionManager.ForegroundSession;
        if (session is null)
        {
            errorMessage = "No foreground session.";
            return false;
        }

        var entities = session.GetEntities();
        var viewportSize = viewport.GetVisibleRect().Size;
        var cameraProj = camera.GetCameraProjection();
        var cameraGlobal = camera.GlobalTransform;

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Camera: {camera.Name} | Viewport: {viewportSize.X:F0}x{viewportSize.Y:F0}");
        sb.AppendLine();

        var godotEntityCount = 0;
        var visibleEntityCount = 0;
        var visibleCount = new NodeCount();

        foreach (var entity in entities)
        {
            if (entity is not GodotSndEntity godotEntity)
                continue;

            godotEntityCount++;
            var before = visibleCount.Total;
            WalkAndReport(godotEntity, godotEntity, cameraGlobal, cameraProj,
                viewportSize, sb, ref visibleCount);

            if (visibleCount.Total > before)
                visibleEntityCount++;
        }

        if (godotEntityCount == 0)
        {
            outputChannel.Publish("No Godot entities available to view.");
            errorMessage = null;
            return true;
        }

        if (visibleCount.Total == 0)
        {
            outputChannel.Publish("No visible entity nodes under the current camera.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{visibleCount.Total} nodes ({visibleCount.ThreeD} 3D, {visibleCount.Ui} UI) "
                + $"visible from {visibleEntityCount} entities.");
            outputChannel.Publish(sb.ToString().TrimEnd());
        }

        errorMessage = null;
        return true;
    }

    private static void WalkAndReport(
        Node node,
        GodotSndEntity owner,
        Transform3D cameraGlobal,
        Projection cameraProj,
        Vector2 viewportSize,
        StringBuilder sb,
        ref NodeCount count)
    {
        var line = FormatNodeLine(node, owner, cameraGlobal, cameraProj, viewportSize, ref count);
        if (line is not null)
        {
            sb.AppendLine(line);
            count.Total++;
        }

        var childCount = node.GetChildCount();
        for (var i = 0; i < childCount; i++)
        {
            WalkAndReport(node.GetChild(i), owner, cameraGlobal, cameraProj,
                viewportSize, sb, ref count);
        }
    }

    private static string? FormatNodeLine(
        Node node,
        GodotSndEntity owner,
        Transform3D cameraGlobal,
        Projection cameraProj,
        Vector2 viewportSize,
        ref NodeCount count)
    {
        switch (node)
        {
            case Node3D node3D:
                {
                    // Only report nodes the engine would render: Visible=false on
                    // the node or a Node3D ancestor removes it from the rendered
                    // frame, so it must not be counted as visible either.
                    if (!node3D.IsVisibleInTree())
                        return null;

                    var result = ProjectionHelper.ProjectWorldToScreen(
                        cameraGlobal, cameraProj, node3D.GlobalPosition, viewportSize);
                    if (result is { } r)
                    {
                        count.ThreeD++;
                        return string.Format(CultureInfo.InvariantCulture,
                            "{0} / {1} [3D] screen=({2:F0}, {3:F0}) depth={4:F1}",
                            owner.StableName, node.Name, r.X, r.Y, r.Z);
                    }

                    return null;
                }
            case Control control:
                {
                    // CanvasItem visibility is independent of the Node3D chain:
                    // the engine hides a Control when its Visible flag or a
                    // CanvasItem ancestor's flag is false.
                    if (!control.IsVisibleInTree())
                        return null;

                    count.Ui++;
                    Vector2 screenPos = control.GlobalPosition;
                    return string.Format(CultureInfo.InvariantCulture,
                        "{0} / {1} [UI] screen=({2:F0}, {3:F0})",
                        owner.StableName, node.Name, screenPos.X, screenPos.Y);
                }
            default:
                return null;
        }
    }
}
