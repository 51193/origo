using System.Globalization;
using System.Text;
using Godot;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Snd;
using Origo.GodotAdapter;

namespace Origo.GodotAdapter.Console;

/// <summary><c>tree_debug &lt;entity&gt;</c> — print the full Godot node tree of an entity.</summary>
internal sealed class TreeDebugCommandHandler(OrigoRuntime runtime) : CommandHandlerBase(runtime)
{
    public override string Name => "tree_debug";

    public override string HelpText => "tree_debug <entity> — print the full node tree of an entity.";

    public override int MinPositionalArgs => 1;

    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();

        var entity = Runtime.SessionManager.ForegroundSession?.FindByName(entityName);
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

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Node tree of entity '{entityName}':");
        PrintTree(godotEntity, sb, 0);
        outputChannel.Publish(sb.ToString().TrimEnd());

        errorMessage = null;
        return true;
    }

    private static void PrintTree(Node node, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}[{node.GetType().Name}] \"{node.Name}\"");

        var count = node.GetChildCount();
        for (var i = 0; i < count; i++)
        {
            var child = node.GetChild(i);
            PrintTree(child, sb, depth + 1);
        }
    }
}
