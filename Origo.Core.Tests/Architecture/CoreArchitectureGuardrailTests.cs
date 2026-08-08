using Origo.Core.Runtime.Lifecycle;
using System;
using System.Linq;
using System.Reflection;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
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
    public void ISndContext_ShouldBeCompositionInterface_WithCompanionProperties()
    {
        var type = typeof(ISndContext);

        var companionProps = new[]
        {
            nameof(ISndContext.Blackboard),
            nameof(ISndContext.Deferred),
            nameof(ISndContext.Template),
            nameof(ISndContext.ConsoleAccess),
            nameof(ISndContext.StateMachines),
            nameof(ISndContext.Save),
            nameof(ISndContext.Lifecycle),
            nameof(ISndContext.FileAccess),
            nameof(ISndContext.ArchiveFileAccess),
            nameof(ISndContext.StateMachineContext)
        };

        foreach (var prop in companionProps)
            Assert.NotNull(type.GetProperty(prop));

        var ownMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName).ToArray();
        Assert.Single(ownMethods);
        Assert.Equal(nameof(ISndContext.Bootstrap), ownMethods[0].Name);
    }

    [Fact]
    public void ISndContext_ShouldExposeAllRoleInterfacesAsCompanionProperties()
    {
        var type = typeof(ISndContext);

        Assert.NotNull(type.GetProperty(nameof(ISndContext.Blackboard)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.Deferred)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.Template)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.ConsoleAccess)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.StateMachines)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.Save)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.Lifecycle)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.FileAccess)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.ArchiveFileAccess)));
        Assert.NotNull(type.GetProperty(nameof(ISndContext.StateMachineContext)));

        var interfaces = type.GetInterfaces();
        Assert.DoesNotContain(typeof(ISndBlackboardAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndDeferredActions), interfaces);
        Assert.DoesNotContain(typeof(ISndTemplateAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndConsoleAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndStateMachineAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndSaveOperations), interfaces);
        Assert.DoesNotContain(typeof(ISndLifecycleOperations), interfaces);
        Assert.DoesNotContain(typeof(ISndFileAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndArchiveFileAccess), interfaces);
        Assert.DoesNotContain(typeof(IStateMachineContext), interfaces);
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
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var fg = (SessionRun?)ctx.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        fg.SessionBlackboard.SetValue("test_key", "test_value");

        ctx.Save.RequestSaveGame("slot_01");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/progress.json"));
    }

    [Fact]
    public void Consumer_AccessesAllRoleInterfaces_ThroughISndContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var fs = new TestMemoryFileSystem();
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(),
            new Blackboard.Blackboard(), fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        var bb = ctx.Blackboard;
        bb.SystemBlackboard.SetValue("consumer_key", "consumer_value");
        var (found, val) = bb.SystemBlackboard.TryGet<string>("consumer_key");
        Assert.True(found);
        Assert.Equal("consumer_value", val);

        var def = ctx.Deferred;
        var executed = false;
        def.EnqueueBusinessDeferred(() => executed = true);
        def.FlushDeferredActionsForCurrentFrame();
        Assert.True(executed);

        var save = ctx.Save;
        Assert.Empty(save.ListSaves());

        var lifecycle = ctx.Lifecycle;
        Assert.False(lifecycle.HasContinueData());

        Assert.NotNull(ctx.Runtime.SessionManager);

        var console = ctx.ConsoleAccess;
        Assert.False(console.TrySubmitConsoleCommand(""));

        ctx.Deferred.FlushDeferredActionsForCurrentFrame();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ISndFileAccess fileAccess = ctx.FileAccess;
        Assert.False(fileAccess.FileExists("nonexistent.json"));
        var writeNode = DataSourceNode.CreateObject();
        writeNode.Add("test_key", DataSourceNode.CreateString("hello"));
        fileAccess.WriteFile("test.json", writeNode);
        Assert.True(fileAccess.FileExists("test.json"));
        var readNode = fileAccess.ReadFile("test.json");
        Assert.Equal("hello", readNode["test_key"].AsString());

        ISndArchiveFileAccess archiveFileAccess = ctx.ArchiveFileAccess;
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
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1_level");
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
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        ctx.Blackboard.ProgressBlackboard!.SetValue("score", 99);
        ctx.Runtime.SessionManager.ForegroundSession!.SessionBlackboard.SetValue("level_data", "xyz");

        ctx.Save.RequestSaveGameAuto();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var saveOps = ctx.Save;
        var saves = saveOps.ListSaves();
        Assert.NotEmpty(saves);

        var lifecycleOps = ctx.Lifecycle;
        Assert.True(lifecycleOps.HasContinueData());
    }

    [Fact]
    public void ISessionRun_ProvidesRuntimeAccess()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("bg1", "bg1_level");

        bg.SessionBlackboard.SetValue("item", "sword");
        var (found, value) = bg.SessionBlackboard.TryGet<string>("item");
        Assert.True(found);
        Assert.Equal("sword", value);

        Assert.Equal("bg1_level", bg.LevelId);
        Assert.False(bg.IsFrontSession);
        Assert.NotNull(bg.GetSessionStateMachines());

        bg.Dispose();
    }

    [Fact]
    public void SessionManager_ProvidesCreateAndDestroyOperations()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
        Assert.NotEmpty(ctx.Runtime.SessionManager.Keys);

        var bg = (SessionRun)ctx.Runtime.SessionManager.CreateBackgroundSession("side", "side_level", true);
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

    [Fact]
    public void SndContext_ShouldNotImplementRoleInterfaces()
    {
        var type = typeof(SndContext);
        var interfaces = type.GetInterfaces();

        Assert.DoesNotContain(typeof(ISndBlackboardAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndDeferredActions), interfaces);
        Assert.DoesNotContain(typeof(ISndTemplateAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndConsoleAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndStateMachineAccess), interfaces);
        Assert.DoesNotContain(typeof(ISndSaveOperations), interfaces);
        Assert.DoesNotContain(typeof(ISndLifecycleOperations), interfaces);
        Assert.DoesNotContain(typeof(IStateMachineContext), interfaces);

        Assert.Contains(typeof(ISndContext), interfaces);
    }

    [Fact]
    public void SndContext_CompanionProperties_ShareConsistentState()
    {
        var runtime = TestFactory.CreateRuntime(new TestLogger(), new TestSndSceneHost());
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));

        Assert.Same(ctx.Blackboard.SystemBlackboard, ctx.StateMachineContext.SystemBlackboard);
        Assert.Same(ctx.Blackboard.ProgressBlackboard, ctx.StateMachineContext.ProgressBlackboard);

        var executed = false;
        ctx.Deferred.EnqueueBusinessDeferred(() => executed = true);
        ctx.StateMachineContext.FlushDeferredActionsForCurrentFrame();
        Assert.True(executed);
    }

    [Fact]
    public void SndEntity_LifecycleMethods_ShouldBeInternal()
    {
        var type = typeof(SndEntity);
        var lifecycleMethods = new[]
        {
            nameof(SndEntity.Process)
        };

        foreach (var methodName in lifecycleMethods)
        {
            var method = type.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Assert.False(method!.IsPublic,
                $"SndEntity.{methodName} must be internal: lifecycle orchestration is driven " +
                "by ISessionRun / SndEntityFactory, not through concrete-type casts.");
        }
    }

    [Fact]
    public void IEntityLifecycle_ShouldBeInternal()
    {
        var type = typeof(IEntityLifecycle);
        Assert.True(type.IsNotPublic,
            "IEntityLifecycle must be internal: business code must not trigger lifecycle " +
            "hooks directly; framework and adapter projects reach it via InternalsVisibleTo.");
    }
}
