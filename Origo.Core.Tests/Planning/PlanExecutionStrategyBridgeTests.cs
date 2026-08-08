using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Planning;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Verifies that the plan engine behaves correctly on adapter-layer bridge
///     entities (entities that delegate to an inner Core <see cref="SndEntity" />
///     but are not themselves <c>SndEntity</c>, e.g. GodotSndEntity). The
///     idempotent action-strategy mount/removal guards must work through the
///     <see cref="IEntityLifecycle" /> contract instead of a concrete-type check.
/// </summary>
[Collection("StrategyStateTests")]
public class PlanExecutionStrategyBridgeTests
{
    private const string _intentKey = "test.intent";
    private const string _intentStatusKey = "test.intent_status";
    private const string _planStepKey = "test.plan_step";
    private const string _actionKey = "test.action";
    private const string _actionStatusKey = "test.action_status";

    [StrategyIndex("test.action.fake")]
    private sealed class FakeActionStrategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex("test.action.fake2")]
    private sealed class FakeAction2Strategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex("test.action.fail")]
    private sealed class ThrowingAddActionStrategy : LifecycleStrategyBase
    {
        public override void AfterAdd(ISndEntity entity, ISndContext ctx)
            => throw new InvalidOperationException("intentional action add failure");
    }

    [StrategyIndex("test.plan_strategy")]
    private sealed class SimplePlanStrategy : PlanExecutionStrategyBase
    {
        protected override string IntentKey => PlanExecutionStrategyBridgeTests._intentKey;
        protected override string IntentStatusKey => PlanExecutionStrategyBridgeTests._intentStatusKey;
        protected override string PlanStepKey => PlanExecutionStrategyBridgeTests._planStepKey;
        protected override string ActionKey => PlanExecutionStrategyBridgeTests._actionKey;
        protected override string ActionStatusKey => PlanExecutionStrategyBridgeTests._actionStatusKey;

        protected override string? ResolveNextStep(string intent, string currentStep, bool failed, ISndEntity entity)
        {
            return (intent, currentStep, failed) switch
            {
                ("test", "" or null, false) => "step_a",
                ("test", "step_a", false) => "step_b",
                ("test", "step_b", false) => null,
                _ => null,
            };
        }

        protected override string? StepToActionIndex(string stepType) => stepType switch
        {
            "step_a" => "test.action.fake",
            "step_b" => "test.action.fake2",
            _ => null,
        };
    }

    [StrategyIndex("test.plan_strategy_fail")]
    private sealed class FailAddPlanStrategy : PlanExecutionStrategyBase
    {
        protected override string IntentKey => PlanExecutionStrategyBridgeTests._intentKey;
        protected override string IntentStatusKey => PlanExecutionStrategyBridgeTests._intentStatusKey;
        protected override string PlanStepKey => PlanExecutionStrategyBridgeTests._planStepKey;
        protected override string ActionKey => PlanExecutionStrategyBridgeTests._actionKey;
        protected override string ActionStatusKey => PlanExecutionStrategyBridgeTests._actionStatusKey;

        protected override string? ResolveNextStep(string intent, string currentStep, bool failed, ISndEntity entity)
        {
            return (intent, currentStep, failed) switch
            {
                ("test", "" or null, false) => "step_fail",
                _ => null,
            };
        }

        protected override string? StepToActionIndex(string stepType) => stepType == "step_fail" ? "test.action.fail" : null;
    }

    private static DelegatingSndEntity SpawnBridgeEntity(
        params string[] lifecycleIndices)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.RegisterStrategy(() => new FakeActionStrategy());
        world.RegisterStrategy(() => new FakeAction2Strategy());
        world.RegisterStrategy(() => new ThrowingAddActionStrategy());
        world.RegisterStrategy(() => new SimplePlanStrategy());
        world.RegisterStrategy(() => new FailAddPlanStrategy());
        host.BindWorld(world);
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("res://entry/entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var ctx = new SndContext(new SndContextParameters(runtime,
            TestFactory.CreateIoGateway(fs),
            TestFactory.CreateFileMetaAccess(fs),
            TestFactory.CreatePathResolver(fs),
            "root", "res://initial", "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = new SndMetaData
        {
            Name = "bridge_entity",
            NodeMetaData = new NodeMetaData(),
            DataMetaData = new DataMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [.. lifecycleIndices] }
        };
        var entity = SndEntityFactory.Spawn(host, meta);
        return new DelegatingSndEntity((SndEntity)entity);
    }

    [Fact]
    public void PlanOnBridgeEntity_StaticLifecycleActionStrategy_ReusesInsteadOfThrowing()
    {
        // The action strategy is mounted statically through LifecycleIndices.
        // On bridge entities the plan engine must detect the existing mount
        // instead of re-adding it and crashing with "already mounted".
        var wrapper = SpawnBridgeEntity("test.plan_strategy", "test.action.fake");

        var ex = Record.Exception(() => wrapper.SetData(_intentKey, "test"));

        Assert.Null(ex);
        var (foundStep, step) = wrapper.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);
        var (foundStatus, status) = wrapper.TryGetData<string>(_actionStatusKey);
        Assert.True(foundStatus);
        Assert.Equal("executing", status);
    }

    [Fact]
    public void PlanOnBridgeEntity_StepSequence_AdvancesAndCleansUpActions()
    {
        // Dynamic mounting path: each step's action strategy is mounted on
        // entry and removed on the transition to the next step.
        var wrapper = SpawnBridgeEntity("test.plan_strategy");
        wrapper.SetData(_intentKey, "test");

        var (foundStep, step) = wrapper.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);

        wrapper.SetData(_actionStatusKey, "completed");

        var (foundStep2, step2) = wrapper.TryGetData<string>(_planStepKey);
        Assert.True(foundStep2);
        Assert.Equal("step_b", step2);

        wrapper.SetData(_actionStatusKey, "completed");

        var (foundIntent, intent) = wrapper.TryGetData<string>(_intentKey);
        Assert.True(foundIntent);
        Assert.Equal(string.Empty, intent);
        var (foundStatus, status) = wrapper.TryGetData<string>(_intentStatusKey);
        Assert.True(foundStatus);
        Assert.Equal("completed", status);
        var (foundAction, action) = wrapper.TryGetData<string>(_actionKey);
        Assert.True(foundAction);
        Assert.Equal(string.Empty, action);

        // Every dynamically mounted action strategy must be released when the
        // plan terminates; only the plan strategy itself remains mounted.
        var meta = ((IEntityLifecycle)wrapper).BuildMetaData();
        Assert.Equal(["test.plan_strategy"], meta.StrategyMetaData!.LifecycleIndices);
    }

    [Fact]
    public void PlanOnBridgeEntity_AfterFailedActionAdd_PlanTerminationIsIdempotent()
    {
        // The action strategy's AfterAdd hook fails, so the strategy is never
        // mounted while PlanStepKey is already written. When the action then
        // reports completion, plan termination must not attempt to remove the
        // never-mounted strategy (which would crash with "not mounted").
        var wrapper = SpawnBridgeEntity("test.plan_strategy_fail");

        Assert.Throws<InvalidOperationException>(() => wrapper.SetData(_intentKey, "test"));
        var (foundStep, step) = wrapper.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_fail", step);

        var ex = Record.Exception(() => wrapper.SetData(_actionStatusKey, "completed"));

        Assert.Null(ex);
        var (foundIntent, intent) = wrapper.TryGetData<string>(_intentKey);
        Assert.True(foundIntent);
        Assert.Equal(string.Empty, intent);
        var (foundStatus, status) = wrapper.TryGetData<string>(_intentStatusKey);
        Assert.True(foundStatus);
        Assert.Equal("completed", status);
    }
}
