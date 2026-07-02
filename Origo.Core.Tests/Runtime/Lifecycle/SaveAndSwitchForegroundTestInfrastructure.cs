using Origo.Core.Runtime.Lifecycle;
using System;
using System.Threading;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Tests;

internal static class SaveAndSwitchForegroundTestInfrastructure
{
    public const string FindByNameStrategyIndex = "test.find_by_name";
    public const string AfterSpawnEventPrefix = "AfterSpawn:";
    public const string AfterLoadEventPrefix = "AfterLoad:";

    internal static (SndContext ctx, TestFileSystem fs) CreateForegroundContext(
        Action<SndWorld>? configureWorld = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        fs.SeedFile("res://entry/entry.json", "[]");
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var tm = new TypeStringMapping();
        var systemBb = new Blackboard.Blackboard();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, systemBb, dataSourceIo);
        configureWorld?.Invoke(runtime.SndWorld);

        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial",
            "res://entry/entry.json"));

        var progressRun = TestFactory.CreateProgressRun(
            "test_save", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);
        progressRun.LoadAndMountForeground("test_level");

        return (ctx, fs);
    }

    public static SndMetaData CreateMeta(string name) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };

    public static SndMetaData CreateMetaWithStrategy(string name, string[]? indices = null) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [.. indices ?? []]
            },
            DataMetaData = new DataMetaData()
        };

    // ── Test strategy: performs FindByName during AfterSpawn/AfterLoad ──

    [StrategyIndex(FindByNameStrategyIndex)]
    public sealed class FindByNameStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _eventSink = new();
        private static List<string>? EventSink { get => _eventSink.Value; set => _eventSink.Value = value; }

        public static void Bind(List<string> sink) => EventSink = sink;

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
        {
            if (EventSink is null)
                return;

            var self = entity.OwningSession.FindByName(entity.Name);
            EventSink.Add($"{AfterSpawnEventPrefix}{entity.Name}:self={(self is not null ? "true" : "false")}");

            foreach (var other in entity.OwningSession.GetEntities())
                if (other.Name != entity.Name)
                    EventSink.Add($"{AfterSpawnEventPrefix}{entity.Name}:sibling={other.Name}");
        }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx)
        {
            if (EventSink is null)
                return;

            var self = entity.OwningSession.FindByName(entity.Name);
            EventSink.Add($"{AfterLoadEventPrefix}{entity.Name}:self={(self is not null ? "true" : "false")}");

            foreach (var other in entity.OwningSession.GetEntities())
                if (other.Name != entity.Name)
                    EventSink.Add($"{AfterLoadEventPrefix}{entity.Name}:sibling={other.Name}");
        }
    }
}
