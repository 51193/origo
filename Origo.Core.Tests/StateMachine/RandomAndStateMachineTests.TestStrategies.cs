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

    [StrategyIndex("sm.pop.orderprobe")]
    private sealed class SmPopOrderProbeStrategy : StateMachineStrategyBase
    {
        private static readonly AsyncLocal<List<string>?> _events = new();
        public static List<string>? Events { get => _events.Value; set => _events.Value = value; }

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add(context.MachineKey);
    }
}
