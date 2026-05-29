using Godot;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Console;

internal sealed class PressButtonCommandHandler : CommandHandlerBase
{
    public PressButtonCommandHandler(OrigoRuntime runtime) : base(runtime)
    {
    }

    public override string Name => "press_button";

    public override string HelpText => "press_button <entity> <path> — 按下指定实体下某路径的 Button 节点";

    public override int MinPositionalArgs => 2;

    public override int MaxPositionalArgs => 2;

    protected override bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();
        var buttonPath = invocation.PositionalArgs[1].Trim();

        var entity = Runtime.Snd.FindByName(entityName);
        if (entity is null)
        {
            errorMessage = $"Entity '{entityName}' not found.";
            return false;
        }

        if (entity is not GodotSndEntity godotEntity)
        {
            errorMessage = $"Entity '{entityName}' is not a Godot entity.";
            return false;
        }

        var button = godotEntity.GetNodeOrNull<Button>(buttonPath);
        if (button is null)
        {
            errorMessage = $"Button not found at path '{buttonPath}' in entity '{entityName}'.";
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        outputChannel.Publish($"Pressed button '{buttonPath}' on entity '{entityName}'.");
        errorMessage = null;
        return true;
    }
}
