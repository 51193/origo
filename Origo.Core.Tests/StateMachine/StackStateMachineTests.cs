using System;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class StackStateMachineTests
{
    private static (SndStrategyPool pool, SndContext ctx) CreatePoolAndContext()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "initial", "entry.json"));
        var pool = runtime.SndWorld.StrategyPool;
        pool.Register(() => new SmPushStub());
        pool.Register(() => new SmPopStub());
        return (pool, ctx);
    }

    [Fact]
    public void Push_ValidValue_SetsPeek()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        sm.Push("state_a");

        var (found, top) = sm.Peek();
        Assert.True(found);
        Assert.Equal("state_a", top);
    }

    [Fact]
    public void Push_MultipleValues_PeekReturnsLast()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        sm.Push("a");
        sm.Push("b");
        sm.Push("c");

        var (found, top) = sm.Peek();
        Assert.True(found);
        Assert.Equal("c", top);
    }

    [Fact]
    public void Push_NullValue_Throws()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        Assert.Throws<ArgumentException>(() => sm.Push(null!));
    }

    [Fact]
    public void Push_EmptyString_Throws()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        Assert.Throws<ArgumentException>(() => sm.Push(""));
    }

    [Fact]
    public void Push_WhitespaceString_Throws()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        Assert.Throws<ArgumentException>(() => sm.Push("   "));
    }

    [Fact]
    public void Push_WhenPushHookThrows_RollsBackPushedValue()
    {
        var (pool, ctx) = CreatePoolAndContext();
        pool.Register(() => new SmThrowOnPush());
        using var sm = new StackStateMachine("test", "sm.push.throw", "sm.pop.stub", pool, ctx.StateMachineContext);

        Assert.Throws<InvalidOperationException>(() => sm.Push("boom"));

        var (found, top) = sm.Peek();
        Assert.False(found);
        Assert.Null(top);
    }

    [Fact]
    public void TryPopRuntime_EmptyStack_ReturnsFalse()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        Assert.False(sm.TryPopRuntime(out _));
    }

    [Fact]
    public void TryPopOnQuit_EmptyStack_ReturnsFalse()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        Assert.False(sm.TryPopOnQuit(out _));
    }

    [Fact]
    public void TryPopRuntime_AfterPush_ReturnsTrueAndPopsTop()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        sm.Push("a");
        sm.Push("b");

        Assert.True(sm.TryPopRuntime(out _));
        var (found, top) = sm.Peek();
        Assert.True(found);
        Assert.Equal("a", top);
    }

    [Fact]
    public void Peek_EmptyStack_ReturnsNull()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        var (found, top) = sm.Peek();
        Assert.False(found);
    }

    [Fact]
    public void Push_AfterDispose_Throws()
    {
        var (pool, ctx) = CreatePoolAndContext();
        var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);
        sm.Dispose();

        var ex = Assert.Throws<ObjectDisposedException>(() => sm.Push("a"));
        Assert.Contains("disposed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryPopRuntime_AfterDispose_Throws()
    {
        var (pool, ctx) = CreatePoolAndContext();
        var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);
        sm.Dispose();

        var ex = Assert.Throws<ObjectDisposedException>(() => sm.TryPopRuntime(out _));
        Assert.Contains("disposed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Peek_AfterDispose_Throws()
    {
        var (pool, ctx) = CreatePoolAndContext();
        var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);
        sm.Dispose();

        var ex = Assert.Throws<ObjectDisposedException>(() => sm.Peek());
        Assert.Contains("disposed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var (pool, ctx) = CreatePoolAndContext();
        var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);
        sm.Dispose();

        var ex = Record.Exception(() => sm.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void RestoreStackWithoutHooks_NullList_Throws()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        Assert.Throws<ArgumentNullException>(() => ((IStateMachine)sm).RestoreStackWithoutHooks(null!));
    }

    [Fact]
    public void RestoreStackWithoutHooks_EmptyList_ResultsInEmptyStack()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        ((IStateMachine)sm).RestoreStackWithoutHooks([]);

        var (found, top) = sm.Peek();
        Assert.False(found);
    }

    [Fact]
    public void RestoreStackWithoutHooks_ThenPeek_ReturnsTop()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        ((IStateMachine)sm).RestoreStackWithoutHooks(["x", "y"]);

        var (found, top) = sm.Peek();
        Assert.True(found);
        Assert.Equal("y", top);
    }

    [Fact]
    public void PushPopPush_RoundTrip_PreservesStackState()
    {
        var (pool, ctx) = CreatePoolAndContext();
        using var sm = new StackStateMachine("test", "sm.push.stub", "sm.pop.stub", pool, ctx.StateMachineContext);

        sm.Push("a");
        sm.Push("b");
        sm.TryPopRuntime(out _);
        sm.Push("c");

        var (found1, top1) = sm.Peek();
        Assert.True(found1);
        Assert.Equal("c", top1);
        sm.TryPopRuntime(out _);
        var (found2, top2) = sm.Peek();
        Assert.True(found2);
        Assert.Equal("a", top2);
    }

    [StrategyIndex("sm.push.stub")]
    private sealed class SmPushStub : StateMachineStrategyBase
    {
    }

    [StrategyIndex("sm.pop.stub")]
    private sealed class SmPopStub : StateMachineStrategyBase
    {
    }

    [StrategyIndex("sm.push.throw")]
    private sealed class SmThrowOnPush : StateMachineStrategyBase
    {
        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            throw new InvalidOperationException("PUSH_BOOM");
    }
}
