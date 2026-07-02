using System;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class SndContextBootstrapTests
{
    [Fact]
    public void Bootstrap_CompletesWithoutError()
    {
        var ctx = CreateBootstrapContext(out var fs);
        fs.SeedFile("entry.json", "[]");

        var ex = Record.Exception(() => ctx.Bootstrap());
        Assert.Null(ex);
    }

    [Fact]
    public void Bootstrap_AfterCall_ForegroundSessionIsEstablished()
    {
        var ctx = CreateBootstrapContext(out var fs);
        fs.SeedFile("entry.json", "[]");

        ctx.Bootstrap();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
    }

    [Fact]
    public void Bootstrap_WithConfigureConverters_CallbackIsInvoked()
    {
        var invoked = false;
        var ctx = CreateBootstrapContext(out var fs,
            configureConverters: _ => invoked = true);
        fs.SeedFile("entry.json", "[]");

        ctx.Bootstrap();

        Assert.True(invoked);
    }

    [Fact]
    public void Bootstrap_AutoDiscoverDisabled_SkipsStrategyDiscovery()
    {
        var ctx = CreateBootstrapContext(out var fs, autoDiscover: false);
        fs.SeedFile("entry.json", "[]");

        ctx.Bootstrap();

        var registered = ctx.Runtime.SndWorld.GetRegisteredStrategyIndices();
        Assert.Empty(registered);
    }

    [Fact]
    public void Bootstrap_WithTemplates_LoadsAndAllowsCloning()
    {
        var ctx = CreateBootstrapContext(out var fs, templateMapPath: "maps/templates.map");
        fs.SeedFile("maps/templates.map", "hero: templates/hero.json");
        fs.SeedFile("templates/hero.json",
            """
            {
                "name": "HeroTemplate",
                "node": { "pairs": {} },
                "strategy": { "lifecycle_indices": [] },
                "data": { "pairs": {} }
            }
            """);
        fs.SeedFile("entry.json", "[]");

        ctx.Bootstrap();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var clone = ctx.Template.CloneTemplate("hero", "MyHero");
        Assert.Equal("MyHero", clone.Name);
    }

    [Fact]
    public void Bootstrap_WithoutEntryJson_ThrowsOnFlush()
    {
        var ctx = CreateBootstrapContext(out _);

        ctx.Bootstrap();

        Assert.ThrowsAny<Exception>(() => ctx.Deferred.FlushDeferredActionsForCurrentFrame());
    }

    [Fact]
    public void SaveRootPath_ReturnsConstructorValue()
    {
        var ctx = CreateBootstrapContext(out _);
        Assert.Equal("root", ctx.SaveRootPath);
    }

    [Fact]
    public void InitialSaveRootPath_ReturnsConstructorValue()
    {
        var ctx = CreateBootstrapContext(out _);
        Assert.Equal("res://initial", ctx.InitialSaveRootPath);
    }

    [Fact]
    public void EntryConfigPath_ReturnsConstructorValue()
    {
        var ctx = CreateBootstrapContext(out _);
        Assert.Equal("entry.json", ctx.EntryConfigPath);
    }

    // ── IStateMachineContext after Bootstrap ──────────────────────────

    [Fact]
    public void IStateMachineContext_SceneAccess_AfterBootstrap_NotNull()
    {
        var ctx = CreateBootstrapContext(out var fs);
        fs.SeedFile("entry.json", "[]");
        ctx.Bootstrap();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var smCtx = ctx.StateMachineContext;
        Assert.NotNull(smCtx.SceneAccess);
    }

    [Fact]
    public void IStateMachineContext_SystemBlackboard_AfterBootstrap_NotNull()
    {
        var ctx = CreateBootstrapContext(out var fs);
        fs.SeedFile("entry.json", "[]");
        ctx.Bootstrap();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var smCtx = ctx.StateMachineContext;
        Assert.NotNull(smCtx.SystemBlackboard);
    }

    [Fact]
    public void IStateMachineContext_ProgressBlackboard_AfterBootstrap_NotNull()
    {
        var ctx = CreateBootstrapContext(out var fs);
        fs.SeedFile("entry.json", "[]");
        ctx.Bootstrap();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        var smCtx = ctx.StateMachineContext;
        Assert.NotNull(smCtx.ProgressBlackboard);
    }

    // ── CloneTemplate edge cases ───────────────────────────────────────

    [Fact]
    public void CloneTemplate_NullKey_ThrowsArgumentException()
    {
        var ctx = CreateBootstrapContext(out _);
        Assert.Throws<ArgumentException>(() => ctx.Template.CloneTemplate(null!));
    }

    [Fact]
    public void CloneTemplate_WhitespaceKey_ThrowsArgumentException()
    {
        var ctx = CreateBootstrapContext(out _);
        Assert.Throws<ArgumentException>(() => ctx.Template.CloneTemplate("   "));
    }

    [Fact]
    public void CloneTemplate_NonExistingKey_Throws()
    {
        var ctx = CreateBootstrapContext(out _);
        Assert.Throws<InvalidOperationException>(() => ctx.Template.CloneTemplate("does_not_exist"));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static SndContext CreateBootstrapContext(
        out TestFileSystem fs,
        bool autoDiscover = false,
        string? templateMapPath = null,
        Action<DataSourceConverterRegistry>? configureConverters = null,
        string[]? skipPrefixes = null)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();
        fs = new TestFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, fs);
        return new SndContext(new SndContextParameters(runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json")
        {
            AutoDiscoverStrategies = autoDiscover,
            DiscoverySkipPrefixes = skipPrefixes,
            SndTemplateMapPath = templateMapPath,
            ConfigureConverters = configureConverters
        });
    }
}
