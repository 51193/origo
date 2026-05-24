using System;
using Godot;
using Origo.Core.Runtime;

namespace Origo.GodotAdapter.Bootstrap;

/// <summary>
///     每帧驱动 <see cref="OrigoRuntime.Console" /> 处理待执行命令（薄桥接，无业务逻辑）。
/// </summary>
public partial class OrigoConsolePump : Node
{
    public OrigoRuntime? Runtime { get; set; }

    public override void _Ready() => SetProcess(true);

    public override void _Process(double delta)
    {
        try
        {
            Runtime?.Console?.ProcessPending();
        }
        catch (Exception ex)
        {
            GD.PushError($"[OrigoConsolePump] Console processing error: {ex.Message}");
        }
    }
}
