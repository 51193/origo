using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Runtime.Console.CommandHandlers;

namespace Origo.Core.Runtime.Console;

/// <summary>
///     控制台门面：从输入队列取行、解析、路由到已注册命令；结果通过通道发布。
/// </summary>
public sealed class OrigoConsole
{
    private readonly IConsoleInputSource _input;
    private readonly ILogger _logger;
    private readonly IConsoleOutputChannel _output;
    private readonly ConsoleCommandRouter _router = new();

    internal static IReadOnlyList<IConsoleCommandHandler> CreateHandlers(OrigoRuntime runtime, ConsoleCommandRouter router)
    {
        return
        [
            new SpawnTemplateCommandHandler(runtime),
            new SndCountCommandHandler(runtime),
            new FindEntityCommandHandler(runtime),
            new KillAllCommandHandler(runtime),
            new BlackboardGetCommandHandler(runtime),
            new BlackboardSetCommandHandler(runtime),
            new BlackboardKeysCommandHandler(runtime),
            new GetEntityDataCommandHandler(runtime),
            new SetEntityDataCommandHandler(runtime),
            new InvokeStrategyCommandHandler(runtime),
            new HelpCommandHandler(router)
        ];
    }

    public OrigoConsole(IConsoleInputSource input, IConsoleOutputChannel output, OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(runtime);
        _input = input;
        _output = output;
        _logger = runtime.Logger;

        var handlers = CreateHandlers(runtime, _router);
        foreach (var h in handlers)
            _router.Register(h);
    }

    public OrigoConsole(IConsoleInputSource input, IConsoleOutputChannel output, OrigoRuntime runtime,
        IEnumerable<IConsoleCommandHandler> extraHandlers)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(extraHandlers);
        _input = input;
        _output = output;
        _logger = runtime.Logger;

        var handlers = CreateHandlers(runtime, _router);
        foreach (var h in handlers)
            _router.Register(h);

        foreach (var h in extraHandlers)
            _router.Register(h);
    }

    /// <summary>
    ///     注册额外的控制台命令处理器（供 <see cref="Snd.SndContext" /> 等延迟创建的组件使用）。
    /// </summary>
    public void RegisterHandler(IConsoleCommandHandler handler) => _router.Register(handler);

    /// <summary>
    ///     处理当前队列中的全部待执行命令（通常每帧或提交时调用一次）。
    /// </summary>
    public void ProcessPending()
    {
        while (_input.TryDequeueCommand(out var line))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cmdWatch = Stopwatch.StartNew();

            _logger.Log(LogLevel.Debug, nameof(OrigoConsole), $"Received command: \"{line}\"");

            if (!ConsoleCommandParser.TryParse(line!, out var invocation, out var parseError))
            {
                _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                    $"Parse error: {parseError ?? "Unknown parse error."}");
                _output.Publish(parseError ?? "Parse error.");
                continue;
            }

            if (invocation is null)
            {
                _logger.Log(LogLevel.Warning, nameof(OrigoConsole),
                    "Internal error: invocation was null after a successful parse.");
                _output.Publish("Internal error: command invocation was null after a successful parse.");
                continue;
            }

            var posArgs = string.Join(", ", invocation.PositionalArgs);
            var namedArgs = string.Join(", ",
                invocation.NamedArgs.Select(kv => $"{kv.Key}={kv.Value}"));
            _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                $"Executing command: \"{invocation.Command}\" (positional: [{posArgs}]; named: {{{namedArgs}}})");

            if (_router.TryExecute(invocation, _output, out var execError))
            {
                _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                    new LogMessageBuilder()
                        .SetElapsedMs(cmdWatch.Elapsed.TotalMilliseconds)
                        .Build($"Command \"{invocation.Command}\" executed successfully."));
            }
            else
            {
                _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                    new LogMessageBuilder()
                        .SetElapsedMs(cmdWatch.Elapsed.TotalMilliseconds)
                        .Build($"Command \"{invocation.Command}\" failed: {execError}"));
                if (!string.IsNullOrEmpty(execError))
                    _output.Publish(execError);
            }
        }
    }
}
