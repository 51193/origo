using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.Core.Tests.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Comprehensive tests for <see cref="StrategyTestScenario.ForActive{T}" />
///     and <see cref="ActiveStrategyTestHarness" />.
/// </summary>
public class ActiveStrategyTestScenarioTests
{
    private const string SimpleStrategyIndex = "active_test.simple";
    private const string InputStrategyIndex = "active_test.with_input";
    private const string DataWriteStrategyIndex = "active_test.data_write";
    private const string DeferredActionStrategyIndex = "active_test.deferred";
    private const string ConsoleCommandStrategyIndex = "active_test.console";
    private const string SaveRequestStrategyIndex = "active_test.save_request";
    private const string TemplateCloneStrategyIndex = "active_test.template";
    private const string ComplexStrategyIndex = "active_test.complex";

    // ── Basic invoke ──────────────────────────────────────────────────

    [Fact]
    public void Invoke_WithNoInput_ReturnsExpectedResult()
    {
        var harness = StrategyTestScenario
            .ForActive<SimpleAnswerStrategy>(SimpleStrategyIndex)
            .Build();

        var result = harness.Invoke();

        Assert.Equal(42, result);
    }

    [Fact]
    public void Invoke_WithInput_PassesInputToStrategy()
    {
        var harness = StrategyTestScenario
            .ForActive<EchoInputStrategy>(InputStrategyIndex)
            .Build();

        var result = harness.Invoke("hello");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Invoke_WithNullInput_StrategyReceivesNull()
    {
        var harness = StrategyTestScenario
            .ForActive<EchoInputStrategy>(InputStrategyIndex)
            .Build();

        var result = harness.Invoke();

        Assert.Null(result);
    }

    [Fact]
    public void Invoke_WithComplexInput_PassesThrough()
    {
        var harness = StrategyTestScenario
            .ForActive<EchoInputStrategy>(InputStrategyIndex)
            .Build();

        var input = new { Key = "test", Value = 99 };
        var result = harness.Invoke(input);

        Assert.Same(input, result);
    }

    // ── Entity data interaction ────────────────────────────────────────

    [Fact]
    public void Strategy_ReadsEntityData_SetViaBuilder()
    {
        var harness = StrategyTestScenario
            .ForActive<DataReadingStrategy>(ComplexStrategyIndex)
            .WithData("counter", 10)
            .WithData("label", "test_label")
            .Build();

        var result = harness.Invoke();

        Assert.Contains("counter=10", result as string);
        Assert.Contains("label=test_label", result as string);
    }

    [Fact]
    public void Strategy_WritesEntityData_HarnessCanReadBack()
    {
        var harness = StrategyTestScenario
            .ForActive<DataWritingStrategy>(DataWriteStrategyIndex)
            .Build();

        harness.Invoke();

        Assert.Equal(1, harness.GetEntityData<int>("invoke_count"));
        Assert.Equal("ok", harness.GetEntityData<string>("invoke_status"));
    }

    [Fact]
    public void MultipleInvokes_IncrementData()
    {
        var harness = StrategyTestScenario
            .ForActive<DataWritingStrategy>(DataWriteStrategyIndex)
            .Build();

        harness.Invoke();
        harness.Invoke();
        harness.Invoke();

        Assert.Equal(3, harness.GetEntityData<int>("invoke_count"));
    }

    [Fact]
    public void GetEntityData_WithMissingKey_Throws()
    {
        var harness = StrategyTestScenario
            .ForActive<SimpleAnswerStrategy>(SimpleStrategyIndex)
            .Build();

        Assert.Throws<InvalidOperationException>(() => harness.GetEntityData<int>("nonexistent"));
    }

    [Fact]
    public void TryGetEntityData_WithMissingKey_ReturnsFalse()
    {
        var harness = StrategyTestScenario
            .ForActive<SimpleAnswerStrategy>(SimpleStrategyIndex)
            .Build();

        var (found, _) = harness.TryGetEntityData<int>("nonexistent");
        Assert.False(found);
    }

    [Fact]
    public void GetEntityData_WithWrongType_Throws()
    {
        var harness = StrategyTestScenario
            .ForActive<DataWritingStrategy>(DataWriteStrategyIndex)
            .Build();
        harness.Invoke();

        Assert.Throws<InvalidOperationException>(() => harness.GetEntityData<string>("invoke_count"));
    }

    // ── InvokeViaEntity path ────────────────────────────────────────────

    [Fact]
    public void InvokeViaEntity_DelegatesToStrategy()
    {
        var harness = StrategyTestScenario
            .ForActive<SimpleAnswerStrategy>(SimpleStrategyIndex)
            .Build();

        var result = harness.InvokeViaEntity();

        Assert.Equal(42, result);
    }

    [Fact]
    public void InvokeViaEntity_WithInput_DelegatesCorrectly()
    {
        var harness = StrategyTestScenario
            .ForActive<EchoInputStrategy>(InputStrategyIndex)
            .Build();

        var result = harness.InvokeViaEntity("world");

        Assert.Equal("world", result);
    }

    // ── Blackboard configuration ────────────────────────────────────────

    [Fact]
    public void SystemConfig_AccessibleInStrategy()
    {
        var harness = StrategyTestScenario
            .ForActive<BlackboardReadingStrategy>(ComplexStrategyIndex)
            .WithSystemConfig("system_key", "sys_value")
            .Build();

        var result = harness.Invoke();

        Assert.Contains("system_key=sys_value", result as string);
        var (sysFound, sysVal) = harness.SystemBlackboard.TryGet<string>("system_key");
        Assert.True(sysFound);
        Assert.Equal("sys_value", sysVal);
    }

    [Fact]
    public void ProgressConfig_AccessibleInStrategy()
    {
        var harness = StrategyTestScenario
            .ForActive<BlackboardReadingStrategy>(ComplexStrategyIndex)
            .WithProgressConfig("progress_key", 777)
            .Build();

        var result = harness.Invoke();

        Assert.Contains("progress_key=777", result as string);
        var (progFound, progVal) = harness.ProgressBlackboard.TryGet<int>("progress_key");
        Assert.True(progFound);
        Assert.Equal(777, progVal);
    }

    [Fact]
    public void SessionConfig_AccessibleInStrategy()
    {
        var harness = StrategyTestScenario
            .ForActive<BlackboardReadingStrategy>(ComplexStrategyIndex)
            .WithSessionConfig("session_key", "ses_val")
            .Build();

        var result = harness.Invoke();

        Assert.Contains("session_key=ses_val", result as string);
        var (sesFound, sesVal) = harness.SessionBlackboard.TryGet<string>("session_key");
        Assert.True(sesFound);
        Assert.Equal("ses_val", sesVal);
    }

    [Fact]
    public void AllThreeBlackboards_Accessible()
    {
        var harness = StrategyTestScenario
            .ForActive<BlackboardReadingStrategy>(ComplexStrategyIndex)
            .WithSystemConfig("a", 1)
            .WithProgressConfig("b", 2)
            .WithSessionConfig("c", 3)
            .Build();

        var result = harness.Invoke();

        Assert.Contains("a=1", result as string);
        Assert.Contains("b=2", result as string);
        Assert.Contains("c=3", result as string);
    }

    // ── Entity name ─────────────────────────────────────────────────────

    [Fact]
    public void DefaultEntityName_IsTestEntity()
    {
        var harness = StrategyTestScenario
            .ForActive<EntityNameStrategy>(ComplexStrategyIndex)
            .Build();

        var result = harness.Invoke();

        Assert.Equal("__test_entity__", result);
    }

    [Fact]
    public void CustomEntityName_PassedToStrategy()
    {
        var harness = StrategyTestScenario
            .ForActive<EntityNameStrategy>(ComplexStrategyIndex)
            .WithEntityName("MyCustomEntity")
            .Build();

        var result = harness.Invoke();

        Assert.Equal("MyCustomEntity", result);
    }

    [Fact]
    public void WithEntityName_EmptyOrWhitespace_ResetsToDefault()
    {
        var harness = StrategyTestScenario
            .ForActive<EntityNameStrategy>(ComplexStrategyIndex)
            .WithEntityName("  ")
            .Build();

        var result = harness.Invoke();

        Assert.Equal("__test_entity__", result);
    }

    // ── Deferred action tracking ────────────────────────────────────────

    [Fact]
    public void Strategy_EnqueueBusinessDeferred_TracksCount()
    {
        var harness = StrategyTestScenario
            .ForActive<BusinessDeferredStrategy>(DeferredActionStrategyIndex)
            .Build();

        Assert.Equal(0, harness.DeferredActionCount);

        harness.Invoke();
        harness.FlushDeferredActions();

        Assert.Equal(1, harness.DeferredActionCount);
    }

    [Fact]
    public void Strategy_MultipleDeferredActions_TracksAll()
    {
        var harness = StrategyTestScenario
            .ForActive<BusinessDeferredStrategy>(DeferredActionStrategyIndex)
            .WithData("defer_count", 3)
            .Build();

        harness.Invoke();
        harness.FlushDeferredActions();

        Assert.Equal(3, harness.DeferredActionCount);
    }

    // ── Console command tracking ────────────────────────────────────────

    [Fact]
    public void Strategy_SubmitConsoleCommand_TracksInList()
    {
        var harness = StrategyTestScenario
            .ForActive<ConsoleCommandStrategy>(ConsoleCommandStrategyIndex)
            .Build();

        Assert.Empty(harness.ConsoleCommands);

        harness.Invoke();

        Assert.Contains("test_command arg1", harness.ConsoleCommands);
    }

    // ── Save / load / level switch request tracking ─────────────────────

    [Fact]
    public void Strategy_RequestSave_TracksRequest()
    {
        var harness = StrategyTestScenario
            .ForActive<SaveRequestStrategy>(SaveRequestStrategyIndex)
            .WithData("save_id", "slot_001")
            .Build();

        Assert.Empty(harness.SaveRequests);

        harness.Invoke();

        Assert.Contains("slot_001", harness.SaveRequests);
    }

    [Fact]
    public void Strategy_RequestLoad_TracksRequest()
    {
        var harness = StrategyTestScenario
            .ForActive<SaveRequestStrategy>(SaveRequestStrategyIndex)
            .WithData("load_id", "slot_002")
            .Build();

        harness.Invoke();

        Assert.Contains("slot_002", harness.LoadRequests);
    }

    [Fact]
    public void Strategy_RequestSwitchLevel_TracksRequest()
    {
        var harness = StrategyTestScenario
            .ForActive<SaveRequestStrategy>(SaveRequestStrategyIndex)
            .WithData("switch_id", "dungeon")
            .Build();

        harness.Invoke();

        Assert.Contains("dungeon", harness.LevelSwitchRequests);
    }

    // ── Template registration ───────────────────────────────────────────

    [Fact]
    public void WithTemplate_RegistersTemplateForCloning()
    {
        var template = new SndMetaData
        {
            Name = "base_template",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };
        template.DataMetaData.Pairs["template_key"] = new TypedData(TypedData.KindMap.String, 0, "template_value");

        var harness = StrategyTestScenario
            .ForActive<TemplateCloneStrategy>(TemplateCloneStrategyIndex)
            .WithTemplate("my_tmpl", template)
            .Build();

        var result = harness.Invoke();

        Assert.Contains("template_key=template_value", result as string);
    }

    // ── Error handling ──────────────────────────────────────────────────

    [Fact]
    public void ForActive_WithNullOrEmptyIndex_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            StrategyTestScenario.ForActive<SimpleAnswerStrategy>(null!));
        Assert.Throws<ArgumentException>(() =>
            StrategyTestScenario.ForActive<SimpleAnswerStrategy>(""));
        Assert.Throws<ArgumentException>(() =>
            StrategyTestScenario.ForActive<SimpleAnswerStrategy>("  "));
    }

    [Fact]
    public void WithTemplate_WithNull_Throws()
    {
        var builder = StrategyTestScenario.ForActive<SimpleAnswerStrategy>(SimpleStrategyIndex);
        Assert.Throws<ArgumentNullException>(() => builder.WithTemplate("key", null!));
    }

    [Fact]
    public void Invoke_StrategyReturnsNull_IsNull()
    {
        var harness = StrategyTestScenario
            .ForActive<NullReturnStrategy>(ComplexStrategyIndex)
            .Build();

        var result = harness.Invoke();

        Assert.Null(result);
    }

    // ── Entity property access after build ──────────────────────────────

    [Fact]
    public void Entity_AfterBuild_IsAccessible()
    {
        var harness = StrategyTestScenario
            .ForActive<SimpleAnswerStrategy>(SimpleStrategyIndex)
            .Build();

        Assert.NotNull(harness.Entity);
        Assert.Equal("__test_entity__", harness.Entity.Name);
    }

    [Fact]
    public void Entity_AfterBuild_StartswithCleanData()
    {
        var harness = StrategyTestScenario
            .ForActive<SimpleAnswerStrategy>(SimpleStrategyIndex)
            .Build();

        var (found, _) = harness.TryGetEntityData<int>("nonexistent");
        Assert.False(found);
    }

    // ── Integration: W3-like food key generation ────────────────────────

    [Fact]
    public void FoodKeyGeneration_Invoke_GeneratesSequentialKeys()
    {
        var harness = StrategyTestScenario
            .ForActive<FoodKeyGeneratorStrategy>(ComplexStrategyIndex)
            .WithData("food.registry", "[]")
            .WithData("food.next_id", 1)
            .Build();

        var key1 = harness.Invoke() as string;
        var key2 = harness.Invoke() as string;
        var key3 = harness.Invoke() as string;

        Assert.NotNull(key1);
        Assert.NotNull(key2);
        Assert.NotNull(key3);
        Assert.StartsWith("Food_", key1);
        Assert.StartsWith("Food_", key2);
        Assert.StartsWith("Food_", key3);
        Assert.NotEqual(key1, key2);
        Assert.NotEqual(key2, key3);
        Assert.Equal(4, harness.GetEntityData<int>("food.next_id"));
    }

    // ── Test strategy implementations ───────────────────────────────────

    [StrategyIndex(SimpleStrategyIndex)]
    private sealed class SimpleAnswerStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => 42;
    }

    [StrategyIndex(InputStrategyIndex)]
    private sealed class EchoInputStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => input;
    }

    [StrategyIndex(DataWriteStrategyIndex)]
    private sealed class DataWritingStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var (found, count) = entity.TryGetData<int>("invoke_count");
            entity.SetData("invoke_count", found ? count + 1 : 1);
            entity.SetData("invoke_status", "ok");
            return null;
        }
    }

    [StrategyIndex(DeferredActionStrategyIndex)]
    private sealed class BusinessDeferredStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            ctx.EnqueueBusinessDeferred(() => { });
            var (found, count) = entity.TryGetData<int>("defer_count");
            var times = found ? count : 1;
            for (var i = 1; i < times; i++)
                ctx.EnqueueBusinessDeferred(() => { });
            return null;
        }
    }

    [StrategyIndex(ConsoleCommandStrategyIndex)]
    private sealed class ConsoleCommandStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            ctx.TrySubmitConsoleCommand("test_command arg1");
            return null;
        }
    }

    [StrategyIndex(SaveRequestStrategyIndex)]
    private sealed class SaveRequestStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var (found, saveId) = entity.TryGetData<string>("save_id");
            if (found && saveId is not null)
                ctx.RequestSaveGameAuto(saveId);

            var (loadFound, loadId) = entity.TryGetData<string>("load_id");
            if (loadFound && loadId is not null)
                ctx.RequestLoadGame(loadId);

            var (switchFound, switchId) = entity.TryGetData<string>("switch_id");
            if (switchFound && switchId is not null)
                ctx.RequestSwitchForegroundLevel(switchId);

            return null;
        }
    }

    [StrategyIndex(TemplateCloneStrategyIndex)]
    private sealed class TemplateCloneStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var clone = ctx.CloneTemplate("my_tmpl", "ClonedEntity");
            if (clone.DataMetaData is null)
                return "no_data";

            var parts = new List<string>();
            foreach (var kv in clone.DataMetaData.Pairs)
                parts.Add($"{kv.Key}={TypedDataObjectConverter.ToObject(kv.Value)}");
            return string.Join(",", parts);
        }
    }

    [StrategyIndex(ComplexStrategyIndex)]
    private sealed class DataReadingStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var parts = new List<string>();
            AppendIfFound(entity, parts, "counter", "counter");
            AppendIfFound(entity, parts, "label", "label");
            return string.Join(",", parts);
        }

        private static void AppendIfFound(ISndEntity entity, List<string> parts, string key, string prefix)
        {
            var (found, value) = entity.TryGetData<object>(key);
            if (found && value is not null)
                parts.Add($"{prefix}={value}");
        }
    }

    [StrategyIndex(ComplexStrategyIndex)]
    private sealed class BlackboardReadingStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var parts = new List<string>();
            AppendFromBb(ctx.SystemBlackboard, parts, "system_key");
            AppendFromBb(ctx.SystemBlackboard, parts, "a");
            AppendFromBb(ctx.ProgressBlackboard!, parts, "progress_key");
            AppendFromBb(ctx.ProgressBlackboard!, parts, "b");
            AppendFromBb(entity.OwningSession.SessionBlackboard, parts, "session_key");
            AppendFromBb(entity.OwningSession.SessionBlackboard, parts, "c");
            return string.Join(",", parts);
        }

        private static void AppendFromBb(IBlackboard bb, List<string> parts, string key)
        {
            var (found, value) = bb.TryGet<string>(key);
            if (found && value is not null)
            {
                parts.Add($"{key}={value}");
                return;
            }

            var (intFound, intValue) = bb.TryGet<int>(key);
            if (intFound)
                parts.Add($"{key}={intValue}");
        }
    }

    [StrategyIndex(ComplexStrategyIndex)]
    private sealed class EntityNameStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => entity.Name;
    }

    [StrategyIndex(ComplexStrategyIndex)]
    private sealed class NullReturnStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => null;
    }

    [StrategyIndex(ComplexStrategyIndex)]
    private sealed class FoodKeyGeneratorStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input)
        {
            var (foundId, nextId) = entity.TryGetData<int>("food.next_id");
            if (!foundId || nextId <= 0)
                return "Food_ffff";

            var key = $"Food_{nextId:x4}";
            entity.SetData("food.next_id", nextId + 1);

            var (foundReg, registry) = entity.TryGetData<string>("food.registry");
            var reg = foundReg && registry is not null ? registry : "[]";
            entity.SetData("food.registry", reg);

            return key;
        }
    }
}
