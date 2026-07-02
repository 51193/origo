using System.Globalization;
using System.Text;
using Godot;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Console;

internal sealed class CameraViewCommandHandler(OrigoRuntime runtime) : CommandHandlerBase(runtime)
{
    public override string Name => "camera_view";

    public override string HelpText => "camera_view — 显示当前摄像头视角下所有可见实体节点的屏幕坐标和深度。";

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
            errorMessage = "无法获取场景树（可能不在运行中的 Godot 引擎内）。";
            return false;
        }

        var viewport = sceneTree.Root;
        var camera = viewport.GetCamera3D();
        if (camera is null)
        {
            errorMessage = "当前视口没有活跃的 Camera3D。";
            return false;
        }

        var session = Runtime.SessionManager.ForegroundSession;
        if (session is null)
        {
            errorMessage = "没有 foreground session。";
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

        var entityCount = 0;
        var visibleCount = new NodeCount();

        foreach (var entity in entities)
        {
            if (entity is not GodotSndEntity godotEntity)
                continue;

            var before = visibleCount.Total;
            WalkAndReport(godotEntity, godotEntity, cameraGlobal, cameraProj,
                viewportSize, sb, ref visibleCount);

            if (visibleCount.Total > before)
                entityCount++;
        }

        if (entityCount == 0)
        {
            outputChannel.Publish("当前没有 Godot 实体可供查看。");
            errorMessage = null;
            return true;
        }

        if (visibleCount.Total == 0)
        {
            outputChannel.Publish("当前摄像头视角内没有可见的实体节点。");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{visibleCount.Total} 个节点 ({visibleCount.ThreeD} 3D, {visibleCount.Ui} UI) "
                + $"来自 {entityCount} 个实体可见。");
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
