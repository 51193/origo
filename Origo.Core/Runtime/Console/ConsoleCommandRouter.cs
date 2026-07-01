using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Console;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     基于字典的手动命令路由。
/// </summary>
internal sealed class ConsoleCommandRouter
{
    private readonly Dictionary<string, IConsoleCommandHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(IConsoleCommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(handler.Name))
            throw new ArgumentException("Handler name cannot be empty.", nameof(handler));

        _handlers[handler.Name] = handler;
    }

    /// <summary>
    ///     返回所有已注册命令名称（有序）。
    /// </summary>
    public IReadOnlyList<string> GetRegisteredNames() =>
        [.. _handlers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    ///     返回所有已注册的命令处理器（有序）。
    /// </summary>
    public IReadOnlyList<IConsoleCommandHandler> GetRegisteredHandlers() => [.. _handlers.Values.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)];

    public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel, out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(outputChannel);

        if (!_handlers.TryGetValue(invocation.Command, out var handler))
        {
            errorMessage = $"Unknown command '{invocation.Command}'.";
            return false;
        }

        return handler.TryExecute(invocation, outputChannel, out errorMessage);
    }
}
