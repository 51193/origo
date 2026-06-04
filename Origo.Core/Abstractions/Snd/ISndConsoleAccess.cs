using System;

namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供控制台命令的提交、处理与输出订阅。
///     策略可通过此接口与外部控制台交互。
/// </summary>
public interface ISndConsoleAccess
{
    /// <summary>提交一条控制台命令。若未注入输入队列则返回 false。</summary>
    bool TrySubmitConsoleCommand(string commandLine);

    /// <summary>处理控制台待执行命令。</summary>
    void ProcessConsolePending();

    /// <summary>订阅控制台输出，返回订阅 ID。</summary>
    long SubscribeConsoleOutput(Action<string> onLine);

    /// <summary>取消控制台输出订阅。</summary>
    void UnsubscribeConsoleOutput(long subscriptionId);
}
