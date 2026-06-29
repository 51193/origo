using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.Core.Testing;
using Xunit;

namespace Origo.Core.Tests;

public class StrategyTestScenarioTests
{
    [Fact]
    public void Process_ModifiesDataAcrossFrames()
    {
        var harness = StrategyTestScenario
            .For<DamageStrategy>("test.damage")
            .WithData("hp", 100)
            .WithData("dps", 10.0)
            .Build();

        harness.RunFrames(5, 1.0);

        Assert.Equal(50, harness.GetEntityData<int>("hp"));
    }

    [Fact]
    public void RunFrame_ExecutesDeferredActions()
    {
        var harness = StrategyTestScenario
            .For<DeferredActionStrategy>("test.deferred")
            .Build();

        harness.RunFrame();

        Assert.Equal(1, harness.DeferredActionCount);
        Assert.Equal(42, harness.GetEntityData<int>("deferred_flag"));
    }

    [Fact]
    public void Build_CallsAfterSpawn()
    {
        var harness = StrategyTestScenario
            .For<AfterSpawnInitStrategy>("test.after_spawn_init")
            .Build();

        Assert.Equal(200, harness.GetEntityData<int>("max_hp"));
    }

    [Fact]
    public void SaveRequest_IsRecorded()
    {
        var harness = StrategyTestScenario
            .For<SaveOnLowHpStrategy>("test.save_on_low")
            .WithData("hp", 5)
            .WithData("dps", 10)
            .Build();

        harness.RunFrame(1.0);

        Assert.Single(harness.SaveRequests);
        Assert.Contains("test_save", harness.SaveRequests);
    }

    [Fact]
    public void LoadRequest_IsRecorded()
    {
        var harness = StrategyTestScenario
            .For<LoadRequestStrategy>("test.load_request")
            .Build();

        harness.RunFrame();

        Assert.Single(harness.LoadRequests);
        Assert.Contains("slot_1", harness.LoadRequests);
    }

    [Fact]
    public void SystemBlackboardConfig_IsAccessible()
    {
        var harness = StrategyTestScenario
            .For<BlackboardReaderStrategy>("test.bb_reader")
            .WithSystemConfig("game.difficulty", "hard")
            .Build();

        harness.RunFrame();

        Assert.Equal("hard", harness.GetEntityData<string>("difficulty"));
    }

    [Fact]
    public void ProgressBlackboardConfig_IsAccessible()
    {
        var harness = StrategyTestScenario
            .For<ProgressBlackboardReaderStrategy>("test.progress_reader")
            .WithProgressConfig("level", 5)
            .Build();

        harness.RunFrame();

        Assert.Equal(5, harness.GetEntityData<int>("current_level"));
    }

    [Fact]
    public void SessionBlackboardConfig_IsAccessible()
    {
        var harness = StrategyTestScenario
            .For<SessionBlackboardReaderStrategy>("test.session_reader")
            .WithSessionConfig("paused", false)
            .Build();

        harness.RunFrame();

        Assert.False(harness.GetEntityData<bool>("is_paused"));
    }

    [Fact]
    public void EntityName_DefaultsAndCanBeOverridden()
    {
        var defaultHarness = StrategyTestScenario
            .For<NopStrategy>("test.nop")
            .Build();

        Assert.Equal("__test_entity__", defaultHarness.Entity.Name);

        var namedHarness = StrategyTestScenario
            .For<NopStrategy>("test.nop")
            .WithEntityName("MyPlayer")
            .Build();

        Assert.Equal("MyPlayer", namedHarness.Entity.Name);
    }

    [Fact]
    public void Template_CanBeRegisteredAndCloned()
    {
        var templateMeta = new SndMetaData
        {
            Name = "EnemyTemplate",
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["type"] = new TypedData(TypedData.KindMap.String, 0, "goblin")
                }
            }
        };

        var harness = StrategyTestScenario
            .For<TemplateCloneStrategy>("test.template_clone")
            .WithTemplate("enemy_template", templateMeta)
            .Build();

        harness.RunFrame();

        Assert.Equal("GoblinKing", harness.GetEntityData<string>("cloned_name"));
        Assert.Equal("goblin", harness.GetEntityData<string>("cloned_type"));
    }

    [Fact]
    public void TriggerLifecycleHooks_ExecuteStrategyHooks()
    {
        var harness = StrategyTestScenario
            .For<LifecycleRecordingStrategy>("test.lifecycle")
            .Build();

        harness.TriggerAfterLoad();
        harness.TriggerBeforeSave();
        harness.TriggerBeforeQuit();

        Assert.Equal(3, harness.GetEntityData<int>("hook_count"));
    }

    [Fact]
    public void LevelSwitchRequest_IsRecorded()
    {
        var harness = StrategyTestScenario
            .For<LevelSwitchStrategy>("test.level_switch")
            .Build();

        harness.RunFrame();

        Assert.Single(harness.LevelSwitchRequests);
        Assert.Contains("level_02", harness.LevelSwitchRequests);
    }

    [Fact]
    public void ConsoleCommand_IsRecorded()
    {
        var harness = StrategyTestScenario
            .For<ConsoleLogStrategy>("test.console")
            .Build();

        harness.RunFrame();

        Assert.Single(harness.ConsoleCommands);
        Assert.Contains("echo hello", harness.ConsoleCommands);
    }

    [Fact]
    public void MultipleFrames_AccumulateCorrectly()
    {
        var harness = StrategyTestScenario
            .For<FrameCounterStrategy>("test.frame_counter")
            .Build();

        harness.RunFrames(100);

        Assert.Equal(100, harness.GetEntityData<int>("frame_count"));
    }

    [Fact]
    public void For_EmptyStrategyIndex_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            StrategyTestScenario.For<NopStrategy>(""));
    }

    [Fact]
    public void TryGetEntityData_ReturnsFalseForMissingKey()
    {
        var harness = StrategyTestScenario
            .For<NopStrategy>("test.nop")
            .Build();

        var (found, value) = harness.TryGetEntityData<int>("missing");

        Assert.False(found);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetEntityData_ReturnsTrueForExistingKey()
    {
        var harness = StrategyTestScenario
            .For<AfterSpawnInitStrategy>("test.after_spawn_init")
            .Build();

        var (found, value) = harness.TryGetEntityData<int>("max_hp");

        Assert.True(found);
        Assert.Equal(200, value);
    }

    [Fact]
    public void TryGetEntityData_ReturnsFalseForTypeMismatch()
    {
        var harness = StrategyTestScenario
            .For<AfterSpawnInitStrategy>("test.after_spawn_init")
            .Build();

        var (found, _) = harness.TryGetEntityData<string>("max_hp");

        Assert.False(found);
    }

    [Fact]
    public void WithEntityName_EmptyString_UsesDefault()
    {
        var harness = StrategyTestScenario
            .For<NopStrategy>("test.nop")
            .WithEntityName("  ")
            .Build();

        Assert.Equal("__test_entity__", harness.Entity.Name);
    }

    [Fact]
    public void WithTemplate_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            StrategyTestScenario
                .For<NopStrategy>("test.nop")
                .WithTemplate("key", null!));
    }

    // ── Test strategies ────────────────────────────────────────────────────

    [StrategyIndex("test.damage")]
    private sealed class DamageStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var (foundHp, hp) = entity.TryGetData<int>("hp");
            if (!foundHp) return;
            var dps = entity.TryGetData<double>("dps").value;
            hp -= (int)(dps * delta);
            entity.SetData("hp", hp);
        }
    }

    [StrategyIndex("test.deferred")]
    private sealed class DeferredActionStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            ctx.EnqueueBusinessDeferred(() => entity.SetData("deferred_flag", 42));
    }

    [StrategyIndex("test.after_spawn_init")]
    private sealed class AfterSpawnInitStrategy : LifecycleStrategyBase
    {
        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => entity.SetData("max_hp", 200);
    }

    [StrategyIndex("test.save_on_low")]
    private sealed class SaveOnLowHpStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var (_, hp) = entity.TryGetData<int>("hp");
            var (_, dps) = entity.TryGetData<int>("dps");
            hp -= dps;
            entity.SetData("hp", hp);
            if (hp <= 0)
                ctx.RequestSaveGame("test_save");
        }
    }

    [StrategyIndex("test.load_request")]
    private sealed class LoadRequestStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => ctx.RequestLoadGame("slot_1");
    }

    [StrategyIndex("test.bb_reader")]
    private sealed class BlackboardReaderStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var (found, difficulty) = ctx.SystemBlackboard.TryGet<string>("game.difficulty");
            if (found)
                entity.SetData("difficulty", difficulty);
        }
    }

    [StrategyIndex("test.progress_reader")]
    private sealed class ProgressBlackboardReaderStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var (found, level) = ctx.ProgressBlackboard!.TryGet<int>("level");
            if (found)
                entity.SetData("current_level", level);
        }
    }

    [StrategyIndex("test.session_reader")]
    private sealed class SessionBlackboardReaderStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var (found, paused) = entity.OwningSession.SessionBlackboard.TryGet<bool>("paused");
            if (found)
                entity.SetData("is_paused", paused);
        }
    }

    [StrategyIndex("test.template_clone")]
    private sealed class TemplateCloneStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var clone = ctx.CloneTemplate("enemy_template", "GoblinKing");
            entity.SetData("cloned_name", clone.Name);
            var (found, type) = clone.DataMetaData!.Pairs.TryGetValue("type", out var td)
                ? (true, td.TryGetString(out var s) ? s : null)
                : (false, null);
            if (found)
                entity.SetData("cloned_type", type);
        }
    }

    [StrategyIndex("test.lifecycle")]
    private sealed class LifecycleRecordingStrategy : LifecycleStrategyBase
    {
        public override void AfterLoad(ISndEntity entity, ISndContext ctx)
        {
            var (found, count) = entity.TryGetData<int>("hook_count");
            entity.SetData("hook_count", found ? count + 1 : 1);
        }

        public override void BeforeSave(ISndEntity entity, ISndContext ctx)
        {
            var (found, count) = entity.TryGetData<int>("hook_count");
            entity.SetData("hook_count", found ? count + 1 : 1);
        }

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
        {
            var (found, count) = entity.TryGetData<int>("hook_count");
            entity.SetData("hook_count", found ? count + 1 : 1);
        }
    }

    [StrategyIndex("test.level_switch")]
    private sealed class LevelSwitchStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            ctx.RequestSwitchForegroundLevel("level_02");
    }

    [StrategyIndex("test.console")]
    private sealed class ConsoleLogStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx) =>
            ctx.TrySubmitConsoleCommand("echo hello");
    }

    [StrategyIndex("test.frame_counter")]
    private sealed class FrameCounterStrategy : LifecycleStrategyBase
    {
        public override void Process(ISndEntity entity, double delta, ISndContext ctx)
        {
            var (found, count) = entity.TryGetData<int>("frame_count");
            entity.SetData("frame_count", found ? count + 1 : 1);
        }
    }

    [StrategyIndex("test.nop")]
    private sealed class NopStrategy : LifecycleStrategyBase
    {
    }
}
