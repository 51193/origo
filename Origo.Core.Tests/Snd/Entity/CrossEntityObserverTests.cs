using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class CrossEntityObserverTests
{
    private const string ObserverProbeIdx = "observer.probe";
    private const string RecordIdx = "observer.record";

    private static FullMemorySndSceneHost CreateHost(Action<SndWorld> configureWorld)
    {
        var logger = new TestLogger();
        var host = new FullMemorySndSceneHost(logger);
        var world = TestFactory.CreateSndWorld(logger: logger);
        configureWorld(world);
        host.BindWorld(world);
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial",
            "res://entry/entry.json"));
        host.BindContext(ctx);
        return host;
    }

    private static SndMetaData CreateMeta(string name, string[]? entityIndices = null,
        string[]? activeIndices = null) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData
        {
            EntityIndices = new List<string>(entityIndices ?? Array.Empty<string>()),
            ActiveIndices = new List<string>(activeIndices ?? Array.Empty<string>())
        },
        DataMetaData = new DataMetaData()
    };

    // ── Self-subscription data ───────────────────────────────────────────

    [Fact]
    public void Subscribe_Self_NotifiedOnDataChange()
    {
        var events = new List<string>();
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", (t, obs, oldVal, newVal) =>
        {
            events.Add($"target={t.Name}");
            events.Add($"observer={obs.Name}");
            events.Add($"old={oldVal}->new={newVal}");
        });

        entity.SetData("hp", 100);
        entity.SetData("hp", 50);

        Assert.Equal(6, events.Count);
        Assert.Contains("target=E", events);
        Assert.Contains("observer=E", events);
        Assert.Contains("old=null->new=(Int32)100", events);
        Assert.Contains("old=(Int32)100->new=(Int32)50", events);
    }

    [Fact]
    public void Unsubscribe_Self_StopsNotification()
    {
        var callCount = 0;
        Action<ISndEntity, ISndEntity, TypedData, TypedData> callback = (_, _, _, _) => callCount++;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", callback);
        entity.SetData("hp", 1);
        Assert.Equal(1, callCount);

        entity.Unsubscribe("hp", callback);
        entity.SetData("hp", 2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Subscribe_Filter_OnlyNotifiesWhenFilterPasses()
    {
        var callCount = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", (_, _, _, _) => callCount++,
            (_, _, _, newVal) => newVal.AsInt32() > 50);

        entity.SetData("hp", 30);
        Assert.Equal(0, callCount);

        entity.SetData("hp", 80);
        Assert.Equal(1, callCount);

        entity.Subscribe("hp", (_, _, _, _) => callCount++);
        entity.SetData("hp", 20);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Subscribe_MultipleKeys_Isolated()
    {
        var hpCount = 0;
        var mpCount = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", (_, _, _, _) => hpCount++);
        entity.Subscribe("mp", (_, _, _, _) => mpCount++);

        entity.SetData("hp", 10);
        entity.SetData("mp", 20);

        Assert.Equal(1, hpCount);
        Assert.Equal(1, mpCount);
    }

    [Fact]
    public void Subscribe_MultipleSubscribers_SameKey_AllNotified()
    {
        var c1 = 0;
        var c2 = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", (_, _, _, _) => c1++);
        entity.Subscribe("hp", (_, _, _, _) => c2++);

        entity.SetData("hp", 50);

        Assert.Equal(1, c1);
        Assert.Equal(1, c2);
    }

    [Fact]
    public void Subscribe_SameValueChange_NotNotified()
    {
        var callCount = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", (_, _, _, _) => callCount++);
        entity.SetData("hp", 50);
        entity.SetData("hp", 50);

        Assert.Equal(1, callCount);
    }

    // ── Cross-entity data observation ────────────────────────────────────

    [Fact]
    public void ObserveData_CrossEntity_NotifiedOnTargetDataChange()
    {
        var events = new List<string>();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new RecordStrategy());
            w.RegisterStrategy(() => new ObserverProbeStrategy());
        });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveData(target, "hp", (t, obs, oldVal, newVal) =>
        {
            events.Add($"target={t.Name}");
            events.Add($"observer={obs.Name}");
            events.Add($"old={oldVal}->new={newVal}");
        });

        target.SetData("hp", 100);
        target.SetData("hp", 50);

        Assert.Equal(6, events.Count);
        Assert.Contains("target=target", events);
        Assert.Contains("observer=observer", events);
        Assert.Contains("old=null->new=(Int32)100", events);
        Assert.Contains("old=(Int32)100->new=(Int32)50", events);
    }

    [Fact]
    public void UnobserveData_CrossEntity_StopsNotification()
    {
        var callCount = 0;
        Action<ISndEntity, ISndEntity, TypedData, TypedData> callback = (_, _, _, _) => callCount++;
        var host = CreateHost(w => { w.RegisterStrategy(() => new ObserverProbeStrategy()); });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target"));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveData(target, "hp", callback);
        target.SetData("hp", 1);
        Assert.Equal(1, callCount);

        observer.UnobserveData(target, "hp", callback);
        target.SetData("hp", 2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void ObserveData_ObserverDoesNotReceiveOwnDataChanges()
    {
        var observerCallCount = 0;
        var targetCallCount = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new ObserverProbeStrategy()); });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target"));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveData(target, "hp", (_, _, _, _) => observerCallCount++);
        target.Subscribe("hp", (_, _, _, _) => targetCallCount++);

        observer.SetData("hp", 50);
        Assert.Equal(0, observerCallCount);
        Assert.Equal(0, targetCallCount);

        target.SetData("hp", 30);
        Assert.Equal(1, observerCallCount);
        Assert.Equal(1, targetCallCount);
    }

    // ── Self lifecycle observation ───────────────────────────────────────

    [Fact]
    public void SubscribeLifecycle_Self_FiresBeforeStrategyHook()
    {
        var events = new List<string>();
        RecordingStrategy.Record = (_, __, evt) => events.Add($"strategy:{evt}");
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new RecordingStrategy());
        });
        var entity = host.CreateEntity(CreateMeta("E", new[] { "observer.recording" }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        events.Clear();

        entity.SubscribeLifecycle((t, obs, evt) =>
        {
            events.Add($"lifecycle:{evt}");
            Assert.Equal("E", t.Name);
            Assert.Equal("E", obs.Name);
        });

        ((IEntityLifecycle)entity).FireBeforeDeadHooks();

        Assert.Equal("lifecycle:BeforeDead", events[0]);
        Assert.Equal("strategy:BeforeDead", events[1]);
    }

    [Fact]
    public void SubscribeLifecycle_Self_AllFiveEventsFire()
    {
        var events = new List<string>();
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));

        entity.SubscribeLifecycle((_, _, evt) => events.Add(evt.ToString()));

        ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        ((IEntityLifecycle)entity).FireAfterLoadHooks();
        ((IEntityLifecycle)entity).FireBeforeSaveHooks();
        ((IEntityLifecycle)entity).FireBeforeQuitHooks();
        ((IEntityLifecycle)entity).FireBeforeDeadHooks();

        Assert.Equal(new[]
        {
            nameof(EntityLifecycleEvent.AfterSpawn),
            nameof(EntityLifecycleEvent.AfterLoad),
            nameof(EntityLifecycleEvent.BeforeSave),
            nameof(EntityLifecycleEvent.BeforeQuit),
            nameof(EntityLifecycleEvent.BeforeDead)
        }, events);
    }

    [Fact]
    public void UnsubscribeLifecycle_Self_StopsNotification()
    {
        var callCount = 0;
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback = (_, _, _) => callCount++;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.SubscribeLifecycle(callback);
        ((IEntityLifecycle)entity).FireBeforeDeadHooks();
        Assert.Equal(1, callCount);

        entity.UnsubscribeLifecycle(callback);
        ((IEntityLifecycle)entity).FireBeforeDeadHooks();
        Assert.Equal(1, callCount);
    }

    // ── Cross-entity lifecycle observation ───────────────────────────────

    [Fact]
    public void ObserveLifecycle_CrossEntity_FiresWhenTargetDies()
    {
        var events = new List<string>();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ObserverProbeStrategy());
            w.RegisterStrategy(() => new RecordStrategy());
        });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveLifecycle(target, (t, obs, evt) =>
        {
            events.Add($"lifecycle_target={t.Name}_obs={obs.Name}_evt={evt}");
        });

        ((IEntityLifecycle)target).FireBeforeDeadHooks();

        Assert.Single(events);
        Assert.Contains("lifecycle_target=target_obs=observer_evt=BeforeDead", events);
    }

    [Fact]
    public void UnobserveLifecycle_CrossEntity_StopsNotification()
    {
        var callCount = 0;
        Action<ISndEntity, ISndEntity, EntityLifecycleEvent> callback = (_, _, _) => callCount++;
        var host = CreateHost(w => { w.RegisterStrategy(() => new ObserverProbeStrategy()); });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target"));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveLifecycle(target, callback);
        ((IEntityLifecycle)target).FireBeforeDeadHooks();
        Assert.Equal(1, callCount);

        observer.UnobserveLifecycle(target, callback);
        ((IEntityLifecycle)target).FireBeforeDeadHooks();
        Assert.Equal(1, callCount);
    }

    // ── Auto-cleanup on observer Teardown ────────────────────────────────

    [Fact]
    public void Teardown_AutoCleansOutgoingDataSubscriptions()
    {
        var callCount = 0;
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ObserverProbeStrategy());
            w.RegisterStrategy(() => new RecordStrategy());
        });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveData(target, "hp", (_, _, _, _) => callCount++);
        target.SetData("hp", 1);
        Assert.Equal(1, callCount);

        ((IEntityLifecycle)observer).TeardownOnly();

        target.SetData("hp", 2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Teardown_AutoCleansOutgoingLifecycleSubscriptions()
    {
        var callCount = 0;
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ObserverProbeStrategy());
            w.RegisterStrategy(() => new RecordStrategy());
        });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveLifecycle(target, (_, _, _) => callCount++);
        ((IEntityLifecycle)observer).TeardownOnly();
        ((IEntityLifecycle)target).FireBeforeDeadHooks();
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Teardown_AutoCleansSelfSubscriptions()
    {
        var dataCount = 0;
        var lifecycleCount = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", (_, _, _, _) => dataCount++);
        entity.SubscribeLifecycle((_, _, _) => lifecycleCount++);

        ((IEntityLifecycle)entity).TeardownOnly();

        entity.SetData("hp", 1);
        ((IEntityLifecycle)entity).FireBeforeDeadHooks();

        Assert.Equal(0, dataCount);
        Assert.Equal(0, lifecycleCount);
    }

    [Fact]
    public void Teardown_Self_ClearsIncomingLifecycleObservers()
    {
        var events = new List<string>();
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var observer = host.CreateEntity(CreateMeta("observer"));
        var target = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveLifecycle(target, (_, _, evt) => events.Add($"obs:{evt}"));
        ((IEntityLifecycle)target).FireBeforeDeadHooks();
        Assert.Contains("obs:BeforeDead", events);

        ((IEntityLifecycle)target).TeardownOnly();
        events.Clear();
        ((IEntityLifecycle)target).FireBeforeDeadHooks();
        Assert.Empty(events);
    }

    // ── Batch scenarios ──────────────────────────────────────────────────

    [Fact]
    public void BatchSpawn_CrossEntityObservation_WorksAfterAllCreated()
    {
        var events = new List<string>();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ObserverProbeStrategy());
            w.RegisterStrategy(() => new RecordStrategy());
        });

        var obsEntity = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var tgtEntity = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));

        obsEntity.ObserveData(tgtEntity, "hp", (_, _, _, _) => events.Add("data"));
        obsEntity.ObserveLifecycle(tgtEntity, (_, _, _) => events.Add("lifecycle"));

        ((IEntityLifecycle)obsEntity).FireAfterSpawnHooks();
        ((IEntityLifecycle)obsEntity).FireAfterSpawnHooks();

        tgtEntity.SetData("hp", 100);
        ((IEntityLifecycle)tgtEntity).FireBeforeDeadHooks();

        Assert.Contains("data", events);
        Assert.Contains("lifecycle", events);
    }

    [Fact]
    public void BatchKill_LifecycleFiresBeforeTeardown()
    {
        var events = new List<string>();
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ObserverProbeStrategy());
            w.RegisterStrategy(() => new RecordStrategy());
        });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveLifecycle(target, (_, _, _) =>
        {
            events.Add("lifecycle_fired");
            Assert.NotNull(host.FindByName("target"));
        });

        ((IEntityLifecycle)target).FireBeforeDeadHooks();
        events.Add("after_hooks");
        ((IEntityLifecycle)target).TeardownOnly();

        host.RemoveEntity("target");
        events.Add("after_remove");

        Assert.Equal(new[] { "lifecycle_fired", "after_hooks", "after_remove" }, events);
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void ObserveData_NullTarget_Throws()
    {
        var host = CreateHost(w => { w.RegisterStrategy(() => new ObserverProbeStrategy()); });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();

        Assert.Throws<NullReferenceException>(() =>
            observer.ObserveData(null!, "hp", (_, _, _, _) => { }));
    }

    [Fact]
    public void Subscribe_NullOrEmptyName_Throws()
    {
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<ArgumentNullException>(() =>
            entity.Subscribe(null!, (_, _, _, _) => { }));
        Assert.Throws<ArgumentException>(() =>
            entity.Subscribe("", (_, _, _, _) => { }));
        Assert.Throws<ArgumentException>(() =>
            entity.Subscribe("  ", (_, _, _, _) => { }));
    }

    [Fact]
    public void Subscribe_NullCallback_Throws()
    {
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        Assert.Throws<ArgumentNullException>(() =>
            entity.Subscribe("hp", null!));
    }

    [Fact]
    public void Unsubscribe_NotSubscribed_DoesNotThrow()
    {
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        var ex = Record.Exception(() =>
            entity.Unsubscribe("hp", (_, _, _, _) => { }));
        Assert.Null(ex);
    }

    [Fact]
    public void UnobserveData_NotObserving_DoesNotThrow()
    {
        var host = CreateHost(w => { w.RegisterStrategy(() => new ObserverProbeStrategy()); });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target"));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();

        var ex = Record.Exception(() =>
            observer.UnobserveData(target, "hp", (_, _, _, _) => { }));
        Assert.Null(ex);
    }

    [Fact]
    public void LifecycleEvent_Order_BeforeStrategyHooks()
    {
        var events = new List<string>();
        OrderedLifecycleStrategy.Events = events;
        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new OrderedLifecycleStrategy());
        });
        var entity = host.CreateEntity(CreateMeta("E", new[] { "observer.ordered" }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();
        events.Clear();

        entity.SubscribeLifecycle((_, _, evt) => events.Add($"lifecycle:{evt}"));

        ((IEntityLifecycle)entity).FireBeforeDeadHooks();

        Assert.Equal("lifecycle:BeforeDead", events[0]);
        Assert.Equal("strategy:BeforeDead", events[1]);
    }

    [Fact]
    public void MultipleLifecycleObservers_AllNotified()
    {
        var c1 = 0;
        var c2 = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new RecordStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { RecordIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.SubscribeLifecycle((_, _, _) => c1++);
        entity.SubscribeLifecycle((_, _, _) => c2++);

        ((IEntityLifecycle)entity).FireBeforeDeadHooks();

        Assert.Equal(1, c1);
        Assert.Equal(1, c2);
    }

    [Fact]
    public void ObserveData_WithFilter_CrossEntity()
    {
        var callCount = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new ObserverProbeStrategy()); });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target"));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveData(target, "hp", (_, _, _, _) => callCount++,
            (_, _, _, newVal) => newVal.AsInt32() > 50);

        target.SetData("hp", 30);
        Assert.Equal(0, callCount);
        target.SetData("hp", 80);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void MethodReference_SubscribeAndUnsubscribe_MatchesSameDelegate()
    {
        var callCount = 0;
        var host = CreateHost(w => { w.RegisterStrategy(() => new ObserverProbeStrategy()); });
        var entity = host.CreateEntity(CreateMeta("E", new[] { ObserverProbeIdx }));
        ((IEntityLifecycle)entity).FireAfterSpawnHooks();

        entity.Subscribe("hp", OnDataChanged);
        entity.SetData("hp", 1);
        Assert.Equal(1, callCount);

        entity.Unsubscribe("hp", OnDataChanged);
        entity.SetData("hp", 2);
        Assert.Equal(1, callCount);
        return;

        void OnDataChanged(ISndEntity t, ISndEntity o, TypedData oldV, TypedData newV) => callCount++;
    }

    [Fact]
    public void FullKillPipeline_ObserverAutoCleanup_PreservesCorrectness()
    {
        var dataEvents = new List<string>();
        var lifecycleEvents = new List<string>();

        void OnData(ISndEntity t, ISndEntity o, TypedData oldV, TypedData newV) =>
            dataEvents.Add($"data:{t.Name}");
        void OnLifecycle(ISndEntity t, ISndEntity o, EntityLifecycleEvent evt) =>
            lifecycleEvents.Add(evt.ToString());

        var host = CreateHost(w =>
        {
            w.RegisterStrategy(() => new ObserverProbeStrategy());
            w.RegisterStrategy(() => new RecordStrategy());
        });
        var observer = host.CreateEntity(CreateMeta("observer", new[] { ObserverProbeIdx }));
        var target = host.CreateEntity(CreateMeta("target", new[] { RecordIdx }));
        ((IEntityLifecycle)observer).FireAfterSpawnHooks();
        ((IEntityLifecycle)target).FireAfterSpawnHooks();

        observer.ObserveData(target, "hp", OnData);
        observer.ObserveLifecycle(target, OnLifecycle);
        target.SetData("hp", 10);
        Assert.Single(dataEvents);

        ((IEntityLifecycle)target).FireBeforeDeadHooks();
        Assert.Contains("BeforeDead", lifecycleEvents);
        ((IEntityLifecycle)target).TeardownOnly();
        host.RemoveEntity("target");

        target.SetData("hp", 20);
        Assert.Single(dataEvents);
    }

    // ── Test strategies ──────────────────────────────────────────────────

    [StrategyIndex(ObserverProbeIdx)]
    private sealed class ObserverProbeStrategy : EntityStrategyBase
    {
    }

    [StrategyIndex(RecordIdx)]
    private sealed class RecordStrategy : EntityStrategyBase
    {
    }

    [StrategyIndex("observer.recording")]
    private sealed class RecordingStrategy : EntityStrategyBase
    {
        public static Action<ISndEntity, ISndContext, EntityLifecycleEvent>? Record { get; set; }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) =>
            Record?.Invoke(entity, ctx, EntityLifecycleEvent.BeforeDead);

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            Record?.Invoke(entity, ctx, EntityLifecycleEvent.BeforeQuit);

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
            Record?.Invoke(entity, ctx, EntityLifecycleEvent.AfterSpawn);
    }

    [StrategyIndex("observer.ordered")]
    private sealed class OrderedLifecycleStrategy : EntityStrategyBase
    {
        public static List<string>? Events { get; set; }

        public override void BeforeDead(ISndEntity entity, ISndContext ctx) =>
            Events?.Add("strategy:BeforeDead");

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            Events?.Add("strategy:BeforeQuit");

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) =>
            Events?.Add("strategy:AfterSpawn");
    }
}
