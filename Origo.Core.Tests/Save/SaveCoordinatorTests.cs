using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     SaveCoordinator 的隔离单元测试。
///     认证构造 null 参数守卫与基本编排路径，完整的 persist/load 工作流由
///     SaveStorageContractTests / SavePolicyContractTests / SaveMetaIntegrationTests 验证。
/// </summary>
public class SaveCoordinatorTests
{
    public enum NullParam
    {
        SessionManager,
        ProgressBlackboard,
        StateMachines,
        ProgressRuntime
    }

    [Theory]
    [InlineData(NullParam.SessionManager, null!, "bb", "sc", "pr")]
    [InlineData(NullParam.ProgressBlackboard, "sm", null!, "sc", "pr")]
    [InlineData(NullParam.StateMachines, "sm", "bb", null!, "pr")]
    [InlineData(NullParam.ProgressRuntime, "sm", "bb", "sc", null!)]
    public void Constructor_NullParam_Throws(NullParam _, string? smMarker, string? bbMarker,
        string? scMarker, string? prMarker)
    {
        var sm = CreateSessionManager();
        var bb = new Blackboard.Blackboard();
        var sc = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());
        var pr = CreateProgressRuntime();

        var sessionManager = smMarker is null ? null! : sm;
        var blackboard = bbMarker is null ? null! : (IBlackboard)bb;
        var stateMachines = scMarker is null ? null! : (IStateMachineContainer)sc;
        var progressRuntime = prMarker is null ? null! : pr;

        Assert.Throws<ArgumentNullException>(() =>
            new SaveCoordinator(sessionManager, blackboard, stateMachines, progressRuntime));
    }

    [Fact]
    public void BuildSessionTopology_SameSessionSet_IsIndependentOfBackgroundCreationOrder()
    {
        var (alphaFirstCoordinator, alphaFirstForeground) =
            CreateCoordinatorWithBackgrounds(alphaFirst: true);
        var (betaFirstCoordinator, betaFirstForeground) =
            CreateCoordinatorWithBackgrounds(alphaFirst: false);

        var alphaFirstTopology = alphaFirstCoordinator.BuildSessionTopology(alphaFirstForeground);
        var betaFirstTopology = betaFirstCoordinator.BuildSessionTopology(betaFirstForeground);

        Assert.Equal(alphaFirstTopology, betaFirstTopology);
        Assert.Equal(
        [
            SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, "default", false),
            SessionTopologyCodec.Serialize("alpha", "level_alpha", false),
            SessionTopologyCodec.Serialize("beta", "level_beta", false)
        ], betaFirstTopology);
    }

    [Fact]
    public void PersistProgress_WithoutForegroundSession_Throws()
    {
        var sm = CreateSessionManager();
        var bb = new Blackboard.Blackboard();
        var sc = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());
        var pr = CreateProgressRuntime();
        var coordinator = new SaveCoordinator(sm, bb, sc, pr);

        var ex = Assert.Throws<InvalidOperationException>(() => coordinator.PersistProgress());
        Assert.Contains("foreground", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static (SaveCoordinator Coordinator, ISessionRun Foreground)
        CreateCoordinatorWithBackgrounds(bool alphaFirst)
    {
        var (sm, progressRuntime) = CreateSessionManagerWithRuntime();
        var bb = new Blackboard.Blackboard();
        var sc = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());
        var coordinator = new SaveCoordinator(sm, bb, sc, progressRuntime);
        var foreground = sm.CreateForegroundSession("default");

        if (alphaFirst)
        {
            sm.CreateBackgroundSession("alpha", "level_alpha");
            sm.CreateBackgroundSession("beta", "level_beta");
        }
        else
        {
            sm.CreateBackgroundSession("beta", "level_beta");
            sm.CreateBackgroundSession("alpha", "level_alpha");
        }

        return (coordinator, foreground);
    }

    private static SessionManager CreateSessionManager() =>
        CreateSessionManagerWithRuntime().Manager;

    private static (SessionManager Manager, ProgressRuntime ProgressRuntime)
        CreateSessionManagerWithRuntime()
    {
        var logger = new TestLogger();
        var bb = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storageService = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var systemParams = new SystemParameters(logger, metaAccess, pathResolver, "root", storageService, new DefaultSavePathPolicy(), runtime.GetAdapterSceneHost());
        var systemRuntime = new SystemRuntime(runtime, systemParams);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
        var progressRuntime = new ProgressRuntime(systemRuntime, new TestStateMachineContext(), ctx);
        return (new SessionManager(progressRuntime, bb), progressRuntime);
    }

    private static ProgressRuntime CreateProgressRuntime()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storageService = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var systemParams = new SystemParameters(logger, metaAccess, pathResolver, "root", storageService, new DefaultSavePathPolicy(), runtime.GetAdapterSceneHost());
        var systemRuntime = new SystemRuntime(runtime, systemParams);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
        return new ProgressRuntime(systemRuntime, new TestStateMachineContext(), ctx);
    }

    private sealed class TestStateMachineContext : IStateMachineContext
    {
        public IBlackboard SystemBlackboard => new Blackboard.Blackboard();
        public IBlackboard ProgressBlackboard => new Blackboard.Blackboard();
        public IBlackboard? SessionBlackboard => null;
        public Abstractions.Scene.ISndSceneReadAccess SceneAccess =>
            new TestSceneAccess();
        public void EnqueueBusinessDeferred(Action action) { }
        public static void FlushDeferredActionsForCurrentFrame() { }
        public int GetPendingPersistenceRequestCount() => 0;
    }

    private sealed class TestSceneAccess : Abstractions.Scene.ISndSceneAccess, Abstractions.Scene.ISndSceneReadAccess
    {
        public IReadOnlyList<SndMetaData> BuildMetaList() =>
            [];
        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList) { }
        public IReadOnlyCollection<ISndEntity> GetEntities() => [];
        public ISndEntity? FindByName(string name) => null;
    }

    private static (IFileMetaAccess MetaAccess, IDataSourceIoGateway DataSourceIo, IPathResolver PathResolver)
        CreateGateways(IFileSystem fs) =>
        (DataSourceFactory.CreateFileMetaAccess(fs),
         DataSourceFactory.CreateDefaultIoGateway(fs),
         DataSourceFactory.CreatePathResolver(fs));
}
