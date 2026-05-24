using System;

namespace Origo.Core.StateMachine;

/// <summary>
///     状态机策略单次回调时的栈上下文：入栈/出栈/读档刷新前后的栈顶快照。
/// </summary>
public readonly struct StateMachineStrategyContext
{
    public StateMachineStrategyContext(string machineKey, string? beforeTop, string? afterTop)
    {
        ArgumentNullException.ThrowIfNull(machineKey);
        MachineKey = machineKey;
        BeforeTop = beforeTop;
        AfterTop = afterTop;
    }

    /// <summary>状态机在容器中的逻辑键。</summary>
    public string MachineKey { get; }

    /// <summary>操作前栈顶元素；空栈时为 null。</summary>
    public string? BeforeTop { get; }

    /// <summary>操作后栈顶元素；若栈被清空则为 null。</summary>
    public string? AfterTop { get; }
}
