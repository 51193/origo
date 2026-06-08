using System;
using System.Reflection;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Runtime;
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
    public void ISndContext_ShouldBeCompositionInterface_WithNoOwnMethodDeclarations()
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
        Assert.Contains(typeof(ISndSessionAccess), interfaces);
        Assert.Contains(typeof(ISndDeferredActions), interfaces);
        Assert.Contains(typeof(ISndTemplateAccess), interfaces);
        Assert.Contains(typeof(ISndConsoleAccess), interfaces);
        Assert.Contains(typeof(ISndStateMachineAccess), interfaces);
        Assert.Contains(typeof(ISndSaveOperations), interfaces);
        Assert.Contains(typeof(ISndLifecycleOperations), interfaces);
        Assert.Contains(typeof(ISndEntityOperations), interfaces);
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
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var fg = ctx.SessionManager.ForegroundSession;
        Assert.NotNull(fg);
        fg.SessionBlackboard.Set("test_key", "test_value");

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/save_slot_01/progress.json"));
    }

    [Fact]
    public void Consumer_AccessesAllRoleInterfaces_ThroughISndContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));

        ISndBlackboardAccess bb = ctx;
        bb.SystemBlackboard.Set("consumer_key", "consumer_value");
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

        ISndSessionAccess session = ctx;
        Assert.NotNull(session.SessionManager);

        ISndConsoleAccess console = ctx;
        Assert.False(console.TrySubmitConsoleCommand(""));

        ISndEntityOperations entityOps = ctx;
        entityOps.RequestKillAll();
        ctx.FlushDeferredActionsForCurrentFrame();
    }

    [Fact]
    public void SessionLifecycle_ManagedThroughISessionManager()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg1", "bg1_level");
        bg.SessionBlackboard.Set("bg_key", "bg_value");

        Assert.True(ctx.SessionManager.Contains("bg1"));
        Assert.NotNull(ctx.SessionManager.TryGet("bg1"));
        Assert.Contains("bg1", ctx.SessionManager.Keys);

        ctx.SessionManager.DestroySession("bg1");
        Assert.False(ctx.SessionManager.Contains("bg1"));
    }

    [Fact]
    public void SaveLoad_TriggeredThroughISndSaveOperations()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestFileSystem();
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        ctx.ProgressBlackboard!.Set("score", 99);
        ctx.CurrentSession!.SessionBlackboard.Set("level_data", "xyz");

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
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        var bg = ctx.SessionManager.CreateBackgroundSession("bg1", "bg1_level");

        bg.SessionBlackboard.Set("item", "sword");
        var (found, value) = bg.SessionBlackboard.TryGet<string>("item");
        Assert.True(found);
        Assert.Equal("sword", value);

        Assert.Equal("bg1_level", bg.LevelId);
        Assert.False(bg.IsFrontSession);
        Assert.NotNull(bg.SceneHost);
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
        var ctx = new SndContext(new SndContextParameters(runtime, fs, "root", "res://initial", "entry.json"));

        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.SessionManager.ForegroundSession);
        Assert.NotEmpty(ctx.SessionManager.Keys);

        var bg = ctx.SessionManager.CreateBackgroundSession("side", "side_level", true);
        Assert.True(ctx.SessionManager.Contains("side"));

        ctx.SessionManager.DestroySession("side");
        Assert.False(ctx.SessionManager.Contains("side"));

        ctx.SessionManager.ProcessAllSessions(0.016);

        bg.Dispose();
    }
}
