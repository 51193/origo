using Godot;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter;

namespace Origo.GodotAdapter.Console;

/// <summary><c>press_button &lt;entity&gt; &lt;path&gt;</c> — simulate pressing a Button node under an entity.</summary>
internal sealed class PressButtonCommandHandler(IOrigoRuntime runtime) : CommandHandlerBase(runtime)
{
    public override string Name => "press_button";

    public override string HelpText => "press_button <entity> <path> — press the Button node at the given path under the entity.";

    public override int MinPositionalArgs => 2;

    public override int MaxPositionalArgs => 2;

    protected override bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();
        var buttonPath = invocation.PositionalArgs[1].Trim();

        if (!TryResolveGodotEntity(entityName, out var godotEntity, out errorMessage))
            return false;

        var button = godotEntity.GetNodeOrNull<Button>(buttonPath);
        if (button is null || !godotEntity.IsAncestorOf(button))
        {
            errorMessage = $"Button not found under entity '{entityName}' at path '{buttonPath}'.";
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        outputChannel.Publish($"Pressed button '{buttonPath}' on entity '{entityName}'.");
        errorMessage = null;
        return true;
    }
}
