using System;
using System.Linq;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Logging;
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

    public OrigoConsole(IConsoleInputSource input, IConsoleOutputChannel output, OrigoRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        _input = input;
        _output = output;
        ArgumentNullException.ThrowIfNull(runtime);
        _logger = runtime.Logger;

        _router.Register(new SpawnTemplateCommandHandler(runtime));
        _router.Register(new SndCountCommandHandler(runtime));
        _router.Register(new HelpCommandHandler(_router));
        _router.Register(new FindEntityCommandHandler(runtime));
        _router.Register(new ClearEntitiesCommandHandler(runtime));
        _router.Register(new BlackboardGetCommandHandler(runtime));
        _router.Register(new BlackboardSetCommandHandler(runtime));
        _router.Register(new BlackboardKeysCommandHandler(runtime));
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

            try
            {
                if (_router.TryExecute(invocation, _output, out var execError))
                {
                    _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                        $"Command \"{invocation.Command}\" executed successfully.");
                }
                else
                {
                    _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                        $"Command \"{invocation.Command}\" failed: {execError}");
                    if (!string.IsNullOrEmpty(execError))
                        _output.Publish(execError);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, nameof(OrigoConsole),
                    $"Command \"{invocation.Command}\" threw exception: {ex.Message}");
                _output.Publish($"Command failed: {ex.Message}");
            }
        }
    }
}
