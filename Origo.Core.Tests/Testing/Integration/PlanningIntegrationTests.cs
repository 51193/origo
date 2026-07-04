using System;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Planning;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class PlanningIntegrationTests
{
    [Fact]
    public void PlanExecution_SetIntent_StartsPlanInFrameLoop()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TwoStepPlanStrategy())
            .WithStrategy(() => new NoopActionStrategy())
            .Build();

        var entity = harness.SpawnEntity("worker", ["test.int.plan.two_step"]);
        entity.SetData("task", "build");
        harness.DriveFrame();

        var (foundStep, step) = entity.TryGetData<string>("plan_step");
        Assert.True(foundStep);
        Assert.Equal("step_a", step);

        var (foundStatus, status) = entity.TryGetData<string>("action_status");
        Assert.True(foundStatus);
        Assert.Equal("executing", status);
    }

    [Fact]
    public void PlanExecution_CompletePlan_InFrameLoop()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TwoStepPlanStrategy())
            .WithStrategy(() => new NoopActionStrategy())
            .Build();

        var entity = harness.SpawnEntity("worker", ["test.int.plan.two_step"]);
        entity.SetData("task", "build");
        harness.DriveFrame();

        entity.SetData("action_status", "completed");
        harness.DriveFrame();

        var (foundStep, step) = entity.TryGetData<string>("plan_step");
        Assert.True(foundStep);
        Assert.Equal("step_b", step);

        entity.SetData("action_status", "completed");
        harness.DriveFrame();

        var (foundIntentStatus, intentStatus) = entity.TryGetData<string>("task_status");
        Assert.True(foundIntentStatus);
        Assert.Equal("completed", intentStatus);
    }

    [Fact]
    public void PlanExecution_WithoutIntent_DoesNotStart()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TwoStepPlanStrategy())
            .WithStrategy(() => new NoopActionStrategy())
            .Build();

        var entity = harness.SpawnEntity("worker", ["test.int.plan.two_step"]);
        harness.DriveFrame();

        Assert.False(entity.TryGetData<string>("plan_step").found);
        Assert.False(entity.TryGetData<string>("task_status").found);
    }

    [Fact]
    public void PlanExecution_DataAttributeKeys_AreSetCorrectly()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TwoStepPlanStrategy())
            .WithStrategy(() => new NoopActionStrategy())
            .Build();

        var entity = harness.SpawnEntity("worker", ["test.int.plan.two_step"]);
        entity.SetData("task", "build");
        harness.DriveFrame();

        Assert.Equal("step_a", entity.GetData<string>("plan_step"));
        var (foundAction, _) = entity.TryGetData<string>("action_index");
        Assert.True(foundAction);
    }

    [Fact]
    public void PlanExecution_MultipleEntities_IndependentPlans()
    {
        var harness = GameplaySimulationHarness.Create()
            .WithStrategy(() => new TwoStepPlanStrategy())
            .WithStrategy(() => new NoopActionStrategy())
            .Build();

        var worker1 = harness.SpawnEntity("worker_1", ["test.int.plan.two_step"]);
        var worker2 = harness.SpawnEntity("worker_2", ["test.int.plan.two_step"]);

        worker1.SetData("task", "build");
        harness.DriveFrame();

        worker2.SetData("task", "repair");
        harness.DriveFrame();

        Assert.Equal("step_a", worker1.GetData<string>("plan_step"));
        Assert.Equal("step_a", worker2.GetData<string>("plan_step"));

        worker1.SetData("action_status", "completed");
        harness.DriveFrame();

        Assert.Equal("step_b", worker1.GetData<string>("plan_step"));
        Assert.Equal("step_a", worker2.GetData<string>("plan_step"));
    }

    // ── test strategies ──────────────────────────────────────────────

    [StrategyIndex("test.int.plan.two_step")]
    private sealed class TwoStepPlanStrategy : PlanExecutionStrategyBase
    {
        protected override string IntentKey => "task";
        protected override string IntentStatusKey => "task_status";
        protected override string PlanStepKey => "plan_step";
        protected override string ActionKey => "action_index";
        protected override string ActionStatusKey => "action_status";

        protected override string? ResolveNextStep(string intent, string currentStep, bool failed, ISndEntity entity)
        {
            return (intent, currentStep) switch
            {
                ("build", "" or null) => "step_a",
                ("build", "step_a") => "step_b",
                ("repair", "" or null) => "step_a",
                ("repair", "step_a") => "step_b",
                _ => null,
            };
        }

        protected override string? StepToActionIndex(string stepType)
        {
            return stepType switch
            {
                "step_a" or "step_b" => "test.int.plan.noop_action",
                _ => null,
            };
        }
    }

    [StrategyIndex("test.int.plan.noop_action")]
    private sealed class NoopActionStrategy : SharedNoopLifecycleStrategy { }
}
