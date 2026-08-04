using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.Core.Runtime;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests for the cross-stage atomic rollback inside
///     <see cref="IEntityLifecycle.RecoverForLifecycle" />: a failure in a
///     later recovery phase must release the strategy pool references and
///     node handles acquired by earlier phases.
/// </summary>
public class SndEntityRecoveryRollbackTests
{
    private const string _lifecycleOnlyIndex = "rollback.lifecycle.only";
    private const string _activeOnlyIndex = "rollback.active.only";

    [Fact]
    public void RecoverForLifecycle_ActivePhaseFails_ReleasesPreviouslyAcquiredPassiveStrategies()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new LifecycleOnlyStrategy());
        runtime.SndWorld.RegisterStrategy(() => new ActiveOnlyStrategy());
        var ctx = CreateContext(runtime);

        var entity = runtime.SndWorld.CreateEntity(
            new TestNodeFactory(), ctx, logger, new ObserverTopology(runtime.SndWorld.StrategyPool, logger));

        // The active phase rejects the index because it names a
        // LifecycleStrategyBase; by then the passive phase already acquired it.
        var meta = CreateMeta(_lifecycleOnlyIndex, _lifecycleOnlyIndex);

        Assert.Throws<InvalidOperationException>(() =>
            ((IEntityLifecycle)entity).RecoverForLifecycle(meta));

        // The cross-stage rollback must release the passive strategy acquired
        // by the earlier phase; otherwise the global pool leaks the reference.
        runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void RecoverForLifecycle_NodePhaseFails_ReleasesCreatedNodesAndNothingFromPool()
    {
        var logger = new TestLogger();
        var runtime = TestFactory.CreateRuntime(logger, new TestSndSceneHost());
        runtime.SndWorld.RegisterStrategy(() => new LifecycleOnlyStrategy());
        var ctx = CreateContext(runtime);
        var nodeFactory = new TestNodeFactory(["res://broken.tscn"]);

        var entity = runtime.SndWorld.CreateEntity(
            nodeFactory, ctx, logger, new ObserverTopology(runtime.SndWorld.StrategyPool, logger));

        var meta = new SndMetaData
        {
            Name = "E",
            NodeMetaData = new NodeMetaData
            {
                Pairs = new Dictionary<string, string>
                {
                    ["ok"] = "res://ok.tscn",
                    ["broken"] = "res://broken.tscn"
                }
            },
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [_lifecycleOnlyIndex],
                ActiveIndices = []
            },
            DataMetaData = new DataMetaData()
        };

        Assert.Throws<InvalidOperationException>(() =>
            ((IEntityLifecycle)entity).RecoverForLifecycle(meta));

        // The first node was created before the second one failed; the node
        // manager's own rollback must have freed it.
        Assert.Single(nodeFactory.CreatedHandles);
        Assert.True(nodeFactory.CreatedHandles[0].FreeCount >= 1,
            "Node created before the failure must be freed by the rollback.");

        // Node-phase failure happens before any strategy acquisition; the
        // cross-stage rollback must not release an unacquired index.
        runtime.SndWorld.StrategyPool.LogPoolLeaks();
        Assert.DoesNotContain(logger.Warnings,
            w => w.Contains("leak", StringComparison.OrdinalIgnoreCase));
    }

    private static SndContext CreateContext(OrigoRuntime runtime)
    {
        var fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        return new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
    }

    private static SndMetaData CreateMeta(string lifecycleIndex, string activeIndex)
    {
        return new SndMetaData
        {
            Name = "E",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = [lifecycleIndex],
                ActiveIndices = [activeIndex]
            },
            DataMetaData = new DataMetaData()
        };
    }

    [StrategyIndex(_lifecycleOnlyIndex)]
    private sealed class LifecycleOnlyStrategy : LifecycleStrategyBase
    {
    }

    [StrategyIndex(_activeOnlyIndex)]
    private sealed class ActiveOnlyStrategy : ActiveStrategyBase
    {
        public override object? Invoke(ISndEntity entity, ISndContext ctx, object? input) => null;
    }
}
