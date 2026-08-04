using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class SndEntityAfterLoadTests
{
    private const string _aIndex = "test.afterload.a";
    private const string _bIndex = "test.afterload.b";

    [Fact]
    public void AfterLoad_EmptyIndices_NoThrow()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());

        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();

        var json = """
                   {
                     "name": "E",
                     "node": { "pairs": {} },
                     "strategy": { "lifecycle_indices": [] },
                     "data": { "pairs": {} }
                   }
                   """;
        var registry = runtime.SndWorld.ConverterRegistry;
        using var node = TestFactory.NodeFromJson(json);
        var meta = registry.Read<SndMetaData>(node);

        var observerTopology = new ObserverTopology(runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(ctx);
        var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger, observerTopology);

        var ex = Record.Exception(() => { ((IEntityLifecycle)entity).RecoverForLifecycle(meta); ((IEntityLifecycle)entity).FireAfterLoadHooks(); });
        Assert.Null(ex);
    }

    [Fact]
    public void AfterLoad_ThrowingStrategy_HookExceptionPropagates()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new ThrowingAfterLoadStrategy());

        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();

        var json = """
                   {
                     "name": "E",
                     "node": { "pairs": {} },
                     "strategy": { "lifecycle_indices": ["test.afterload.throwing"] },
                     "data": { "pairs": {} }
                   }
                   """;
        var registry = runtime.SndWorld.ConverterRegistry;
        using var node = TestFactory.NodeFromJson(json);
        var meta = registry.Read<SndMetaData>(node);

        var observerTopology = new ObserverTopology(runtime.SndWorld.StrategyPool, logger);
        observerTopology.BindContext(ctx);
        var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger, observerTopology);

        Assert.Throws<InvalidOperationException>(() => { ((IEntityLifecycle)entity).RecoverForLifecycle(meta); ((IEntityLifecycle)entity).FireAfterLoadHooks(); });
    }

    [Fact]
    public void SndEntity_Load_FromJson_InvokesAfterLoad_ForAllStrategies_InIndexOrder()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new AfterLoadProbeAStrategy());
        runtime.SndWorld.RegisterStrategy(() => new AfterLoadProbeBStrategy());

        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var nodeFactory = new TestNodeFactory();

        AfterLoadProbeAStrategy.Events = [];

        try
        {
            var json = """
                       {
                         "name": "E",
                         "node": { "pairs": {} },
                          "strategy": { "lifecycle_indices": ["test.afterload.a", "test.afterload.b"] },
                         "data": { "pairs": {} }
                       }
                       """;
            var registry = runtime.SndWorld.ConverterRegistry;
            using var node = TestFactory.NodeFromJson(json);
            var meta = registry.Read<SndMetaData>(node);

            var observerTopology = new ObserverTopology(runtime.SndWorld.StrategyPool, logger);
            observerTopology.BindContext(ctx);
            var entity = runtime.SndWorld.CreateEntity(nodeFactory, ctx, logger, observerTopology);
            ((IEntityLifecycle)entity).RecoverForLifecycle(meta); ((IEntityLifecycle)entity).FireAfterLoadHooks();

            Assert.Equal(new[] { "afterload:a", "afterload:b" }, AfterLoadProbeAStrategy.Events);
        }
        finally
        {
            AfterLoadProbeAStrategy.Events = null;
        }
    }

    [StrategyIndex(_aIndex)]
    private sealed class AfterLoadProbeAStrategy : LifecycleStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void AfterLoad(ISndEntity entity, ISndContext ctx) => Events?.Add("afterload:a");
    }

    [StrategyIndex(_bIndex)]
    private sealed class AfterLoadProbeBStrategy : LifecycleStrategyBase
    {
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) =>
            AfterLoadProbeAStrategy.Events?.Add("afterload:b");
    }

    [StrategyIndex("test.afterload.throwing")]
    private sealed class ThrowingAfterLoadStrategy : LifecycleStrategyBase
    {
        public override void AfterLoad(ISndEntity entity, ISndContext ctx) =>
            throw new InvalidOperationException("afterload failure");
    }
}
