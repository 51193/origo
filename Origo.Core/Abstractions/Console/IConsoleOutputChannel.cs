using System;

namespace Origo.Core.Abstractions.Console;

/// <summary>
///     Core 侧控制台输出发布通道（无本地历史状态）。
///     Core 发布字符串消息，适配层/策略按生命周期订阅和取消订阅。
/// </summary>
public interface IConsoleOutputChannel
{
    /// <summary>
    ///     注册输出监听器，返回订阅 id。
    /// </summary>
    long Subscribe(Action<string> listener);

    /// <summary>
    ///     取消指定订阅；若 id 不存在返回 false。
    /// </summary>
    bool Unsubscribe(long subscriptionId);

    /// <summary>
    ///     发布一条控制台输出消息。
    /// </summary>
    void Publish(string line);
}
