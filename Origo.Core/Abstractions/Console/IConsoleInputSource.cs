using System.Diagnostics.CodeAnalysis;

namespace Origo.Core.Abstractions.Console;

/// <summary>
///     Core 侧与适配层之间的控制台输入抽象。适配层通过 <see cref="Enqueue" /> 投递命令，
///     Core 通过 <see cref="TryDequeueCommand" /> 按帧消费。
/// </summary>
public interface IConsoleInputSource
{
    /// <summary>
    ///     尝试从队列中取出一行待解析的命令文本；无输入时返回 false。
    /// </summary>
    bool TryDequeueCommand([NotNullWhen(true)] out string? line);

    /// <summary>
    ///     向队列中追加一条命令行文本。空白行将被忽略。
    /// </summary>
    void Enqueue(string line);

    /// <summary>
    ///     清空队列中的所有待处理命令。
    /// </summary>
    void Clear();
}
