using System;
using System.Reflection;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Tests;

public class CoreArchitectureGuardrailTests
{
    [Fact]
    public void CoreAssembly_ShouldNotReferenceGodot()
    {
        var refs = typeof(OrigoRuntime).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs,
            r => r.Name != null && r.Name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ISndContext_ShouldBeCompositionInterface_WithMinimalOwnDeclarations()
    {
        var type = typeof(ISndContext);
        var ownMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var ownProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        Assert.Empty(ownMethods);
        Assert.Empty(ownProperties);
    }

    [Fact]
    public void ISndContext_ShouldInheritAllRoleInterfaces()
    {
        var type = typeof(ISndContext);
        var interfaces = type.GetInterfaces();

        Assert.Contains(typeof(ISndBlackboardAccess), interfaces);
        Assert.Contains(typeof(ISndDeferredActions), interfaces);
        Assert.Contains(typeof(ISndTemplateAccess), interfaces);
        Assert.Contains(typeof(ISndConsoleAccess), interfaces);
        Assert.Contains(typeof(ISndStateMachineAccess), interfaces);
        Assert.Contains(typeof(ISndSaveOperations), interfaces);
        Assert.Contains(typeof(ISndLifecycleOperations), interfaces);
        Assert.Contains(typeof(ISndFileAccess), interfaces);
        Assert.Contains(typeof(ISndArchiveFileAccess), interfaces);
    }

    [Fact]
    public void IStateMachineContext_ShouldInheritSharedRoleInterfaces()
    {
        var type = typeof(IStateMachineContext);
        var interfaces = type.GetInterfaces();

        Assert.Contains(typeof(ISndBlackboardAccess), interfaces);
        Assert.Contains(typeof(ISndDeferredActions), interfaces);

        var ownMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(ownMethods, m => m.Name == "get_SystemBlackboard");
        Assert.DoesNotContain(ownMethods, m => m.Name == "get_ProgressBlackboard");
        Assert.DoesNotContain(ownMethods, m => m.Name == "EnqueueBusinessDeferred");
    }

    // ── Behavioral replacements for reflection-based implementation-detail tests ──

    [Fact]
    public void Consumer_UsingOnlyPublicInterfaces_CanPerformSaveLoadWorkflow()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        fg.SessionBlackboard.SetValue("test_key", "test_value");

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/progress.json"));
    }

    [Fact]
    public void Consumer_AccessesAllRoleInterfaces_ThroughISndContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestFileSystem();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        ISndBlackboardAccess bb = ctx;
        bb.SystemBlackboard.SetValue("consumer_key", "consumer_value");
        var (found, val) = bb.SystemBlackboard.TryGet<string>("consumer_key");
        Assert.True(found);
        Assert.Equal("consumer_value", val);

        ISndDeferredActions def = ctx;
        var executed = false;
        def.EnqueueBusinessDeferred(() => executed = true);
        def.FlushDeferredActionsForCurrentFrame();
        Assert.True(executed);

        ISndSaveOperations save = ctx;
        Assert.Empty(save.ListSaves());

        ISndLifecycleOperations lifecycle = ctx;
        Assert.False(lifecycle.HasContinueData());

        Assert.NotNull(ctx.Runtime.SessionManager);

        ISndConsoleAccess console = ctx;
        Assert.False(console.TrySubmitConsoleCommand(""));

        ctx.FlushDeferredActionsForCurrentFrame();
        ctx.FlushDeferredActionsForCurrentFrame();

        ISndFileAccess fileAccess = ctx;
        Assert.False(fileAccess.FileExists("nonexistent.json"));
        var writeNode = DataSourceNode.CreateObject();
        writeNode.Add("test_key", DataSourceNode.CreateString("hello"));
        fileAccess.WriteFile("test.json", writeNode);
        Assert.True(fileAccess.FileExists("test.json"));
        var readNode = fileAccess.ReadFile("test.json");
        Assert.Equal("hello", readNode["test_key"].AsString());

        ISndArchiveFileAccess archiveFileAccess = ctx;
        Assert.False(archiveFileAccess.FileExists("nonexistent.json"));
        var archiveNode = DataSourceNode.CreateObject();
        archiveNode.Add("archive_key", DataSourceNode.CreateString("world"));
        archiveFileAccess.WriteFile("archive_test.json", archiveNode);
        Assert.True(archiveFileAccess.FileExists("archive_test.json"));
        var archiveReadNode = archiveFileAccess.ReadFile("archive_test.json");
        Assert.Equal("world", archiveReadNode["archive_key"].AsString());
    }

    [Fact]
    public void SessionLifecycle_ManagedThroughISessionManager()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1_level");
        bg.SessionBlackboard.SetValue("bg_key", "bg_value");

        Assert.True(ctx.Runtime.SessionManager.Contains("bg1"));
        Assert.NotNull(ctx.Runtime.SessionManager.TryGet("bg1"));
        Assert.Contains("bg1", ctx.Runtime.SessionManager.Keys);

        ctx.Runtime.SessionManager.DestroySession("bg1");
        Assert.False(ctx.Runtime.SessionManager.Contains("bg1"));
    }

    [Fact]
    public void SaveLoad_TriggeredThroughISndSaveOperations()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.ProgressBlackboard!.SetValue("score", 99);
        ctx.Runtime.SessionManager.ForegroundSession!.SessionBlackboard.SetValue("level_data", "xyz");

        ctx.RequestSaveGameAuto();
        ctx.FlushDeferredActionsForCurrentFrame();

        ISndSaveOperations saveOps = ctx;
        var saves = saveOps.ListSaves();
        Assert.NotEmpty(saves);

        ISndLifecycleOperations lifecycleOps = ctx;
        Assert.True(lifecycleOps.HasContinueData());
    }

    [Fact]
    public void ISessionRun_ProvidesRuntimeAccess()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1_level");

        bg.SessionBlackboard.SetValue("item", "sword");
        var (found, value) = bg.SessionBlackboard.TryGet<string>("item");
        Assert.True(found);
        Assert.Equal("sword", value);

        Assert.Equal("bg1_level", bg.LevelId);
        Assert.False(bg.IsFrontSession);
        Assert.NotNull(((SessionRun)bg).SceneHost);
        Assert.NotNull(bg.GetSessionStateMachines());

        bg.Dispose();
    }

    [Fact]
    public void SessionManager_ProvidesCreateAndDestroyOperations()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.NotEmpty(ctx.Runtime.SessionManager.Keys);

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("side", "side_level", true);
        Assert.True(ctx.Runtime.SessionManager.Contains("side"));

        ctx.Runtime.SessionManager.DestroySession("side");
        Assert.False(ctx.Runtime.SessionManager.Contains("side"));

        ctx.Runtime.SessionManager.ProcessAllSessions(0.016);

        bg.Dispose();
    }

    [Fact]
    public void ConsoleCommandHandlerBase_ShouldBePublic_SoExternalProjectsCanExtendIt()
    {
        var type = typeof(Origo.Core.Runtime.Console.ConsoleCommandHandlerBase);
        Assert.True(type.IsPublic || type.IsNestedPublic,
            "ConsoleCommandHandlerBase must be public so external projects " +
            "(such as origo.demo) can derive custom console command handlers.");
    }
}
