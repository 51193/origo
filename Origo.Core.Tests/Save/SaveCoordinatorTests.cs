using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
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
    [Fact]
    public void Constructor_NullSessionManager_Throws()
    {
        var bb = new Blackboard.Blackboard();
        var sm = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());
        var pr = CreateProgressRuntime();

        Assert.Throws<ArgumentNullException>(() =>
            new SaveCoordinator(null!, bb, sm, pr, "save_01"));
    }

    [Fact]
    public void Constructor_NullProgressBlackboard_Throws()
    {
        var sm = CreateSessionManager();
        var sc = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());
        var pr = CreateProgressRuntime();

        Assert.Throws<ArgumentNullException>(() =>
            new SaveCoordinator(sm, null!, sc, pr, "save_01"));
    }

    [Fact]
    public void Constructor_NullStateMachines_Throws()
    {
        var sm = CreateSessionManager();
        var bb = new Blackboard.Blackboard();
        var pr = CreateProgressRuntime();

        Assert.Throws<ArgumentNullException>(() =>
            new SaveCoordinator(sm, bb, null!, pr, "save_01"));
    }

    [Fact]
    public void Constructor_NullProgressRuntime_Throws()
    {
        var sm = CreateSessionManager();
        var bb = new Blackboard.Blackboard();
        var sc = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());

        Assert.Throws<ArgumentNullException>(() =>
            new SaveCoordinator(sm, bb, sc, null!, "save_01"));
    }

    [Fact]
    public void Constructor_NullSaveId_Throws()
    {
        var sm = CreateSessionManager();
        var bb = new Blackboard.Blackboard();
        var sc = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());
        var pr = CreateProgressRuntime();

        Assert.Throws<ArgumentNullException>(() =>
            new SaveCoordinator(sm, bb, sc, pr, null!));
    }

    [Fact]
    public void PersistProgress_WithoutForegroundSession_Throws()
    {
        var sm = CreateSessionManager();
        var bb = new Blackboard.Blackboard();
        var sc = new StateMachineContainer(new SndStrategyPool(new TestLogger()), new TestStateMachineContext());
        var pr = CreateProgressRuntime();
        var coordinator = new SaveCoordinator(sm, bb, sc, pr, "test_save");

        var ex = Assert.Throws<InvalidOperationException>(() => coordinator.PersistProgress());
        Assert.Contains("foreground", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SessionManager CreateSessionManager()
    {
        var logger = new TestLogger();
        var bb = new Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var (metaAccess, dataSourceIo, pathResolver) = CreateGateways(fs);
        var storageService = new DefaultSaveStorageService(metaAccess, dataSourceIo, pathResolver, "root");
        var systemParams = new SystemParameters(logger, metaAccess, pathResolver, "root", storageService, new DefaultSavePathPolicy(), runtime.GetAdapterSceneHost());
        var systemRuntime = new SystemRuntime(runtime, systemParams);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
        var progressRuntime = new ProgressRuntime(systemRuntime, new TestStateMachineContext(), ctx);
        return new SessionManager(progressRuntime, bb);
    }

    private static ProgressRuntime CreateProgressRuntime()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
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
        public Abstractions.Scene.ISndSceneAccess SceneAccess =>
            new TestSceneAccess();
        public void EnqueueBusinessDeferred(Action action) { }
        public void FlushDeferredActionsForCurrentFrame() { }
        public int GetPendingPersistenceRequestCount() => 0;
    }

    private sealed class TestSceneAccess : Abstractions.Scene.ISndSceneAccess
    {
        public IReadOnlyList<SndMetaData> BuildMetaList() =>
            Array.Empty<SndMetaData>();
        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList) { }
    }

    private static (IFileMetaAccess MetaAccess, IDataSourceIoGateway DataSourceIo, IPathResolver PathResolver)
        CreateGateways(IFileSystem fs) =>
        (DataSourceFactory.CreateFileMetaAccess(fs),
         DataSourceFactory.CreateDefaultIoGateway(fs),
         DataSourceFactory.CreatePathResolver(fs));
}
