using System;
using System.Threading;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Planning;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
public class PlanExecutionStrategyBaseTests
{
    // ── Test strategy: minimal plan ─────────────────────────────────

    private const string _intentKey = "test.intent";
    private const string _intentStatusKey = "test.intent_status";
    private const string _planStepKey = "test.plan_step";
    private const string _actionKey = "test.action";
    private const string _actionStatusKey = "test.action_status";

    [StrategyIndex("test.action.fake")]
    private sealed class FakeActionStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _afterAddCalls = new();
        public static List<string>? AfterAddCalls { get => _afterAddCalls.Value; set => _afterAddCalls.Value = value; }

        private static readonly AsyncLocal<List<string>?> _beforeRemoveCalls = new();
        public static List<string>? BeforeRemoveCalls { get => _beforeRemoveCalls.Value; set => _beforeRemoveCalls.Value = value; }

        public override void AfterAdd(ISndEntity entity, ISndContext ctx) => AfterAddCalls?.Add(entity.Name);

        public override void BeforeRemove(ISndEntity entity, ISndContext ctx) => BeforeRemoveCalls?.Add(entity.Name);
    }

    [StrategyIndex("test.action.fake2")]
    private sealed class FakeAction2Strategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _afterAddCalls = new();
        public static List<string>? AfterAddCalls { get => _afterAddCalls.Value; set => _afterAddCalls.Value = value; }

        public override void AfterAdd(ISndEntity entity, ISndContext ctx) => AfterAddCalls?.Add(entity.Name);
    }

    [StrategyIndex("test.plan_strategy")]
    private sealed class SimplePlanStrategy : PlanExecutionStrategyBase
    {
        protected override string IntentKey => PlanExecutionStrategyBaseTests._intentKey;
        protected override string IntentStatusKey => PlanExecutionStrategyBaseTests._intentStatusKey;
        protected override string PlanStepKey => PlanExecutionStrategyBaseTests._planStepKey;
        protected override string ActionKey => PlanExecutionStrategyBaseTests._actionKey;
        protected override string ActionStatusKey => PlanExecutionStrategyBaseTests._actionStatusKey;

        protected override string? ResolveNextStep(string intent, string currentStep, bool failed, ISndEntity entity)
        {
            return (intent, currentStep, failed) switch
            {
                ("test", "" or null, false) => "step_a",
                ("test", "step_a", false) => "step_b",
                ("test", "step_b", false) => null,
                ("step_refuses_action", "" or null, false) => "no_action_step",
                _ => null,
            };
        }

        protected override string? StepToActionIndex(string stepType)
        {
            return stepType switch
            {
                "step_a" => "test.action.fake",
                "step_b" => "test.action.fake2",
                _ => null,
            };
        }
    }

    // ── Tests: default hooks ────────────────────────────────────────

    [Fact]
    public void DefaultHooks_DoNotMutateEntityData()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData("score", 42);
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterSpawn(entity, ctx);
        strategy.AfterLoad(entity, ctx);
        strategy.AfterAdd(entity, ctx);
        strategy.BeforeRemove(entity, ctx);
        strategy.BeforeQuit(entity, ctx);
        strategy.BeforeDead(entity, ctx);

        Assert.Equal(42, entity.GetData<int>("score"));
    }

    [Fact]
    public void AfterSpawn_IntentPresent_StartsPlan()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData(_intentKey, "test");
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterSpawn(entity, ctx);

        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);

        var (foundStatus, status) = entity.TryGetData<string>(_actionStatusKey);
        Assert.True(foundStatus);
        Assert.Equal("executing", status);
    }

    [Fact]
    public void AfterSpawn_NoIntent_DoesNotStartPlan()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterSpawn(entity, ctx);

        var (foundStep, _) = entity.TryGetData<string>(_planStepKey);
        Assert.False(foundStep);

        var (foundStatus, _) = entity.TryGetData<string>(_actionStatusKey);
        Assert.False(foundStatus);
    }

    [Fact]
    public void AfterLoad_IntentPresent_DoesNotRestartPlan()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData(_intentKey, "test");
        entity.SetData(_planStepKey, "step_b");
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterLoad(entity, ctx);

        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_b", step);
    }

    [Fact]
    public void AfterAdd_IntentPresent_StartsPlan()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData(_intentKey, "test");
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterAdd(entity, ctx);

        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);
    }

    [Fact]
    public void StartIntent_ClearsPreviousPlanState()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData(_intentKey, "test");
        entity.SetData(_planStepKey, "old_step");
        entity.SetData(_actionKey, "old_action");
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterSpawn(entity, ctx);

        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);
    }

    // ── Tests: StepToActionIndex returns null ──────────────────────

    [Fact]
    public void StepWithoutAction_DoesNotAddStrategy()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData(_intentKey, "step_refuses_action");
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterSpawn(entity, ctx);

        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("no_action_step", step);

        var (foundStatus, status) = entity.TryGetData<string>(_actionStatusKey);
        Assert.True(foundStatus);
        Assert.Equal("executing", status);
    }

    // ── Tests: plan advancement via subscription on real entities ──

    [Fact]
    public void ActionCompletion_InSndEntity_AdvancesToNextStep()
    {
        FakeActionStrategy.AfterAddCalls = [];
        FakeActionStrategy.BeforeRemoveCalls = [];
        FakeAction2Strategy.AfterAddCalls = [];

        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.StrategyPool.Register(() => new SimplePlanStrategy());
        world.StrategyPool.Register(() => new FakeActionStrategy());
        world.StrategyPool.Register(() => new FakeAction2Strategy());
        host.BindWorld(world);

        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = new SndMetaData
        {
            Name = "test_entity",
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [] },
            DataMetaData = new DataMetaData(),
            NodeMetaData = new NodeMetaData()
        };

        var entity = host.CreateEntity(meta);
        entity.SetData(_intentKey, "test");

        entity.AddStrategy("test.plan_strategy");

        // After AddStrategy, AfterAdd fires → Wire(true) → StartIntent
        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);
        Assert.Single(FakeActionStrategy.AfterAddCalls);
        Assert.Equal("test_entity", FakeActionStrategy.AfterAddCalls![0]);

        // Complete step_a → should advance to step_b
        entity.SetData(_actionStatusKey, "completed");

        var (foundStep2, step2) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep2);
        Assert.Equal("step_b", step2);

        Assert.Single(FakeActionStrategy.BeforeRemoveCalls!);
        Assert.Single(FakeAction2Strategy.AfterAddCalls!);
    }

    [Fact]
    public void ActionCompletion_LastStep_CompletesPlan()
    {
        FakeActionStrategy.AfterAddCalls = [];
        FakeActionStrategy.BeforeRemoveCalls = [];
        FakeAction2Strategy.AfterAddCalls = [];

        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.StrategyPool.Register(() => new SimplePlanStrategy());
        world.StrategyPool.Register(() => new FakeActionStrategy());
        world.StrategyPool.Register(() => new FakeAction2Strategy());
        host.BindWorld(world);

        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = new SndMetaData
        {
            Name = "test_entity",
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [] },
            DataMetaData = new DataMetaData(),
            NodeMetaData = new NodeMetaData()
        };

        var entity = host.CreateEntity(meta);
        entity.SetData(_intentKey, "test");

        entity.AddStrategy("test.plan_strategy");

        // Plan started at step_a
        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);

        // Complete step_a → advances to step_b
        entity.SetData(_actionStatusKey, "completed");

        // Complete step_b → plan finishes, intent cleared
        entity.SetData(_actionStatusKey, "completed");

        var (foundIntent, intent) = entity.TryGetData<string>(_intentKey);
        Assert.True(foundIntent);
        Assert.Equal("", intent);

        var (foundStatus, intentStatus) = entity.TryGetData<string>(_intentStatusKey);
        Assert.True(foundStatus);
        Assert.Equal("completed", intentStatus);
    }

    // ── Tests: before hooks remove action ──────────────────────────

    [Fact]
    public void BeforeRemove_UnmountsActionStrategy()
    {
        FakeActionStrategy.AfterAddCalls = [];
        FakeActionStrategy.BeforeRemoveCalls = [];

        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.StrategyPool.Register(() => new SimplePlanStrategy());
        world.StrategyPool.Register(() => new FakeActionStrategy());
        host.BindWorld(world);

        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = new SndMetaData
        {
            Name = "test_entity",
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [] },
            DataMetaData = new DataMetaData(),
            NodeMetaData = new NodeMetaData()
        };

        var entity = host.CreateEntity(meta);
        entity.SetData(_intentKey, "test");

        entity.AddStrategy("test.plan_strategy");

        Assert.Single(FakeActionStrategy.AfterAddCalls!);

        // Remove the plan strategy → BeforeRemove fires → RemoveCurrentAction → remove action strategy
        entity.RemoveStrategy("test.plan_strategy");

        Assert.Single(FakeActionStrategy.BeforeRemoveCalls!);
        Assert.Equal("test_entity", FakeActionStrategy.BeforeRemoveCalls![0]);
    }

    // ── Tests: failure path ────────────────────────────────────────

    [StrategyIndex("test.fail_plan_strategy")]
    private sealed class FailingPlanStrategy : PlanExecutionStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _completedCalls = new();
        public static List<string>? CompletedCalls { get => _completedCalls.Value; set => _completedCalls.Value = value; }

        private static readonly AsyncLocal<List<string>?> _failedCalls = new();
        public static List<string>? FailedCalls { get => _failedCalls.Value; set => _failedCalls.Value = value; }

        protected override string IntentKey => PlanExecutionStrategyBaseTests._intentKey;
        protected override string IntentStatusKey => PlanExecutionStrategyBaseTests._intentStatusKey;
        protected override string PlanStepKey => PlanExecutionStrategyBaseTests._planStepKey;
        protected override string ActionKey => PlanExecutionStrategyBaseTests._actionKey;
        protected override string ActionStatusKey => PlanExecutionStrategyBaseTests._actionStatusKey;

        protected override string? ResolveNextStep(string intent, string currentStep, bool failed, ISndEntity entity)
        {
            return (intent, currentStep, failed) switch
            {
                ("fail_test", "" or null, false) => "step_a",
                ("fail_test", "step_a", true) => null,
                _ => null,
            };
        }

        protected override string? StepToActionIndex(string stepType)
        {
            return stepType switch
            {
                "step_a" => "test.action.fake",
                _ => null,
            };
        }

        protected override void OnPlanCompleted(ISndEntity entity) => CompletedCalls?.Add(entity.Name);

        protected override void OnPlanFailed(ISndEntity entity) => FailedCalls?.Add(entity.Name);
    }

    [Fact]
    public void ActionFailed_AdvancesPlan_AndTerminates()
    {
        FailingPlanStrategy.CompletedCalls = [];
        FailingPlanStrategy.FailedCalls = [];
        FakeActionStrategy.AfterAddCalls = [];
        FakeActionStrategy.BeforeRemoveCalls = [];

        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        world.StrategyPool.Register(() => new FailingPlanStrategy());
        world.StrategyPool.Register(() => new FakeActionStrategy());
        host.BindWorld(world);

        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, host);
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "res://entry/entry.json"));
        host.BindContext(ctx);

        var meta = new SndMetaData
        {
            Name = "test_entity",
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [] },
            DataMetaData = new DataMetaData(),
            NodeMetaData = new NodeMetaData()
        };

        var entity = host.CreateEntity(meta);
        entity.SetData(_intentKey, "fail_test");

        entity.AddStrategy("test.fail_plan_strategy");

        // Plan started at step_a
        var (foundStep, step) = entity.TryGetData<string>(_planStepKey);
        Assert.True(foundStep);
        Assert.Equal("step_a", step);

        // Action fails → plan advances with failed=true → ResolveNextStep returns null → plan terminates
        entity.SetData(_actionStatusKey, "failed");

        var (foundIntent, intent) = entity.TryGetData<string>(_intentKey);
        Assert.True(foundIntent);
        Assert.Equal("", intent);

        Assert.Single(FailingPlanStrategy.FailedCalls!);
        Assert.Equal("test_entity", FailingPlanStrategy.FailedCalls![0]);
        Assert.Empty(FailingPlanStrategy.CompletedCalls!);
    }

    [Fact]
    public void Wire_CalledTwice_DoesNotLeakSubscriptions()
    {
        var strategy = new SimplePlanStrategy();
        var entity = new StubSndEntity("e");
        entity.SetData(_intentKey, "test");
        ISndContext ctx = NullSndContext.Instance;

        strategy.AfterSpawn(entity, ctx);
        Assert.Equal(1, entity.GetRawSubscriptionCount(_intentKey));
        Assert.Equal(1, entity.GetRawSubscriptionCount(_actionStatusKey));

        strategy.AfterAdd(entity, ctx);
        Assert.Equal(1, entity.GetRawSubscriptionCount(_intentKey));
        Assert.Equal(1, entity.GetRawSubscriptionCount(_actionStatusKey));

        strategy.BeforeRemove(entity, ctx);
        Assert.Equal(0, entity.GetRawSubscriptionCount(_intentKey));
        Assert.Equal(0, entity.GetRawSubscriptionCount(_actionStatusKey));
    }

    // ── Cleanup ────────────────────────────────────────────────────

    public PlanExecutionStrategyBaseTests()
    {
        FakeActionStrategy.AfterAddCalls = null;
        FakeActionStrategy.BeforeRemoveCalls = null;
        FakeAction2Strategy.AfterAddCalls = null;
        FailingPlanStrategy.CompletedCalls = null;
        FailingPlanStrategy.FailedCalls = null;
    }
}
