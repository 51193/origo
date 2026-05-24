using System.Diagnostics.CodeAnalysis;

namespace Origo.Core.Abstractions.Console;

/// <summary>
///     Core 侧从适配层获取待执行控制台命令行的抽象（轮询式，无 UI 依赖）。
/// </summary>
public interface IConsoleInputSource
{
    /// <summary>
    ///     尝试从队列中取出一行待解析的命令文本；无输入时返回 false。
    /// </summary>
    bool TryDequeueCommand([NotNullWhen(true)] out string? line);
}
