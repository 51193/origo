using System;
using Godot;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter;

namespace ShellPackageConsumer;

/// <summary>
///     Minimal demo strategy discovered from the package-consumer assembly. It
///     proves that custom gameplay strategies live in consumer code while Core
///     owns discovery/validation, the entity data pipeline, and save hooks.
/// </summary>
[StrategyIndex("demo.tick")]
public sealed class DemoStrategy : LifecycleStrategyBase
{
    /// <inheritdoc/>
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        var node = entity.GetNodeFromSnd<Node2D>("main")
            ?? throw new InvalidOperationException(
                "A strategy could not resolve the authored Godot node through the inner entity.");
        entity.SetData("node_name", (string)node.Name);
        if (!entity.TryGetData<int>("hp").found)
            entity.SetData("hp", 100);
        entity.SetData("spawned", "yes");
    }

    /// <inheritdoc/>
    public override void BeforeSave(ISndEntity entity, ISndContext ctx) =>
        entity.SetData("saved_by_hook", "yes");

    /// <inheritdoc/>
    public override void AfterLoad(ISndEntity entity, ISndContext ctx)
    {
        var node = entity.GetNodeFromSnd<Node2D>("main")
            ?? throw new InvalidOperationException(
                "A strategy could not resolve the authored Godot node after load.");
        entity.SetData("node_name_after_load", (string)node.Name);
        entity.SetData("loaded_by_hook", "yes");
    }
}
