using System.Globalization;
using System.Text;
using Godot;
using Origo.Core.Abstractions.Console;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Console;

internal sealed class TreeDebugCommandHandler : CommandHandlerBase
{
    public TreeDebugCommandHandler(OrigoRuntime runtime) : base(runtime)
    {
    }

    public override string Name => "tree_debug";

    public override string HelpText => "tree_debug <entity> — 打印实体的完整节点树。";

    public override int MinPositionalArgs => 1;

    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(CommandInvocation invocation, IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        var entityName = invocation.PositionalArgs[0].Trim();

        var entity = Runtime.Snd.FindByName(entityName);
        if (entity is null)
        {
            errorMessage = $"实体 '{entityName}' 未找到。";
            return false;
        }

        if (entity is not GodotSndEntity godotEntity)
        {
            errorMessage = $"实体 '{entityName}' 不是 Godot 实体。";
            return false;
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"实体 '{entityName}' 的节点树：");
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
