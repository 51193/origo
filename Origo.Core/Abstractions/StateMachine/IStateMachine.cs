using System.Collections.Generic;

namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     字符串栈状态机：栈内仅存储字符串，Push/Pop 语义由策略钩子实现。
/// </summary>
public interface IStateMachine
{
    string MachineKey { get; }

    string PushStrategyIndex { get; }

    string PopStrategyIndex { get; }

    void Push(string value);

    /// <summary>
    ///     运行时出栈：触发 pop 策略的 <c>BeforeRemove</c> 语义。
    /// </summary>
    bool TryPopRuntime(out string? popped);

    /// <summary>
    ///     退出逐级出栈：触发 pop 策略的 <c>BeforeQuit</c> 语义。
    /// </summary>
    bool TryPopOnQuit(out string? popped);

    (bool found, string? top) Peek();

    /// <summary>
    ///     自栈底至栈顶的字符串快照。
    /// </summary>
    IReadOnlyList<string> Snapshot();

    /// <summary>
    ///     读档恢复后，在场景构建完成时按入栈顺序调用 Push 策略的 <c>AfterLoad</c>。
    /// </summary>
    void FlushAfterLoad();

    /// <summary>
    ///     从存档恢复栈内容，不触发任何策略钩子。
    /// </summary>
    void RestoreStackWithoutHooks(IReadOnlyList<string> stackBottomToTop);
}
