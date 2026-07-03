using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Tests;

internal static class DisposeSemanticsTestInfrastructure
{
    public const string BeforeSaveStrategyIndex = "dispose_sem.before_save";
    public const string BeforeQuitStrategyIndex = "dispose_sem.before_quit";
    public const string SessionAccessStrategyIndex = "dispose_sem.session_access";
    public const string ThrowingQuitStrategyIndex = "dispose_sem.throwing_quit";

    public static (SndContext ctx, TestFileSystem fs) CreateForegroundContext(
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

    public static SndMetaData CreateMetaWithIndex(string name, string index) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [index] },
            DataMetaData = new DataMetaData()
        };

    [StrategyIndex(BeforeSaveStrategyIndex)]
    public sealed class BeforeSaveSpyStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static void Bind(List<string> events) => _events.Value = events;

        public override void BeforeSave(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"BeforeSave:{entity.Name}");
    }

    [StrategyIndex(BeforeQuitStrategyIndex)]
    public sealed class BeforeQuitSpyStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static void Bind(List<string> events) => _events.Value = events;

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            _events.Value?.Add($"BeforeQuit:{entity.Name}");
    }

    [StrategyIndex(SessionAccessStrategyIndex)]
    public sealed class SessionAccessQuitStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();

        public static void Bind(List<string> events) => _events.Value = events;

        public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
        {
            var session = entity.OwningSession;
            if (session == null) return;

            try
            {
                session.FindByName(entity.Name);
                _events.Value?.Add("SceneHostAccess:OK");
            }
            catch (ObjectDisposedException)
            {
                _events.Value?.Add("SceneHostAccess:DISPOSED");
            }

            try
            {
                var _ = session.SessionBlackboard;
                _events.Value?.Add("BlackboardAccess:OK");
            }
            catch (ObjectDisposedException)
            {
                _events.Value?.Add("BlackboardAccess:DISPOSED");
            }
        }
    }

    [StrategyIndex(ThrowingQuitStrategyIndex)]
    public sealed class ThrowingQuitStrategy : LifecycleStrategyBase
    {
        public override void BeforeQuit(ISndEntity entity, ISndContext ctx) =>
            throw new InvalidOperationException("Intentional BeforeQuit failure for testing.");
    }
}
