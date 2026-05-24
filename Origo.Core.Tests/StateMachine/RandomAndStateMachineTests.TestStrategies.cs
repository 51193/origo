using System.Collections.Generic;
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
        public static List<string>? PushEvents { get; set; }
        public static List<string>? AfterLoadEvents { get; set; }

        public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            PushEvents?.Add($"push:runtime:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");

        public override void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            AfterLoadEvents?.Add($"push:afterload:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
    }

    [StrategyIndex("sm.pop.test")]
    private sealed class SmPopStrategy : StateMachineStrategyBase
    {
        public static List<string>? PopRemoveEvents { get; set; }
        public static List<string>? PopQuitEvents { get; set; }

        public override void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            PopRemoveEvents?.Add($"pop:runtime:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            PopQuitEvents?.Add($"pop:beforeQuit:{context.BeforeTop ?? "null"}->{context.AfterTop ?? "null"}");
    }

    [StrategyIndex("sm.pop.orderprobe")]
    private sealed class SmPopOrderProbeStrategy : StateMachineStrategyBase
    {
        public static List<string>? Events { get; set; }

        public override void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) =>
            Events?.Add(context.MachineKey);
    }
}
