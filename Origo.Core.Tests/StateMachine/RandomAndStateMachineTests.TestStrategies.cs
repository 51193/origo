using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Snd.Strategy;
using Origo.Core.StateMachine;

namespace Origo.Core.Tests;

public partial class RandomAndStateMachineTests
{
    private static void ResetStrategyHooks()
    {
        SmPushStrategy.PushEvents = null;
        SmPushStrategy.AfterLoadEvents = null;
        SmPopStrategy.PopRemoveEvents = null;
        SmPopStrategy.PopQuitEvents = null;
        SmPopOrderProbeStrategy.Events = null;
        SmThrowOnTopQuitStrategy.Events = null;
        SmPushThenThrowOnQuitStrategy.Events = null;
        SmPushThenThrowOnQuitStrategy.Reset();
        SmPushThenReturnOnQuitStrategy.Events = null;
        SmPushThenReturnOnQuitStrategy.Reset();
    }

    [StrategyIndex("sm.push.test")]
    private sealed class SmPushStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _pushEvents = new();
        public static List<string>? PushEvents { get => _pushEvents.Value; set => _pushEvents.Value = value; }
        private static readonly AsyncLocal<List<string>?> _afterLoadEvents = new();
        public static List<string>? AfterLoadEvents { get => _afterLoadEvents.Value; set => _afterLoadEvents.Value = value; }

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            PushEvents?.Add($"push:runtime:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");

        public override void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            AfterLoadEvents?.Add($"push:afterload:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
    }

    [StrategyIndex("sm.pop.test")]
    private sealed class SmPopStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _popRemoveEvents = new();
        public static List<string>? PopRemoveEvents { get => _popRemoveEvents.Value; set => _popRemoveEvents.Value = value; }
        private static readonly AsyncLocal<List<string>?> _popQuitEvents = new();
        public static List<string>? PopQuitEvents { get => _popQuitEvents.Value; set => _popQuitEvents.Value = value; }

        public override void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            PopRemoveEvents?.Add($"pop:runtime:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            PopQuitEvents?.Add($"pop:beforeQuit:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
    }

    [StrategyIndex("sm.pop.throw")]
    private sealed class SmThrowOnQuitStrategy : StateMachineStrategyBase
    {
        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            throw new InvalidOperationException("quit hook failure");
    }

    [StrategyIndex("sm.pop.orderprobe")]
    private sealed class SmPopOrderProbeStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add(context.MachineKey);
    }

    /// <summary>
    ///     Records each quit-time pop and throws only when the popped top is
    ///     "boom", so a test can prove that a throwing quit hook does not strand
    ///     the lower levels of the same machine.
    /// </summary>
    [StrategyIndex("sm.pop.throw_on_top")]
    private sealed class SmThrowOnTopQuitStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx)
        {
            Events?.Add($"quit:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
            if (context.BeforeTop == "boom")
                throw new InvalidOperationException("quit hook failure on top level");
        }
    }

    /// <summary>
    ///     Mutates the stack (pushes a level) and then throws on the first
    ///     quit-time pop of "boom". Proves that force-popping after a throwing
    ///     hook uses the pre-hook snapshot, so the machine terminates instead of
    ///     re-running the failing level forever, and the lower levels still run
    ///     their quit hooks.
    /// </summary>
    [StrategyIndex("sm.pop.push_then_throw")]
    private sealed class SmPushThenThrowOnQuitStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        private static readonly AsyncLocal<bool?> _pushed = new();

        public static StackStateMachine? Machine { get; set; }

        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx)
        {
            Events?.Add($"quit:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
            if (context.BeforeTop != "boom" || _pushed.Value == true)
                return;

            _pushed.Value = true;
            Machine!.Push("extra");
            throw new InvalidOperationException("quit hook pushed a level then threw");
        }

        public static void Reset()
        {
            Machine = null;
            _pushed.Value = false;
        }
    }

    /// <summary>
    ///     Mutates the stack (pushes a level) and then returns normally on the
    ///     first quit-time pop of "boom". Proves that a no-progress pop is
    ///     detected from the pre-hook snapshot, so quit terminates with bounded
    ///     hook invocations instead of repeatedly popping the pushed level, and
    ///     the lower levels still run their quit hooks.
    /// </summary>
    [StrategyIndex("sm.pop.push_then_return")]
    private sealed class SmPushThenReturnOnQuitStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        private static readonly AsyncLocal<bool?> _pushed = new();

        public static StackStateMachine? Machine { get; set; }

        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx)
        {
            Events?.Add($"quit:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
            if (context.BeforeTop != "boom" || _pushed.Value == true)
                return;

            _pushed.Value = true;
            Machine!.Push("extra");
        }

        public static void Reset()
        {
            Machine = null;
            _pushed.Value = false;
        }
    }
}
