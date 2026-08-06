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
///     Console facade: pulls lines from the input queue, parses them, routes to registered commands,
///     and publishes results through the output channel.
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

    /// <summary>
    ///     Creates the console with the built-in command handlers registered.
    /// </summary>
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

    /// <summary>
    ///     Creates the console with built-in handlers plus extra handlers registered.
    /// </summary>
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
    ///     Registers additional console command handlers (for use by lazily-created components such as
    ///     <see cref="Snd.SndContext" />).
    /// </summary>
    public void RegisterHandler(IConsoleCommandHandler handler) => _router.Register(handler);

    /// <summary>
    ///     Processes all pending commands currently in the queue (typically invoked once per frame or on commit).
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

            var inv = invocation!;

            var posArgs = string.Join(", ", inv.PositionalArgs);
            var namedArgs = string.Join(", ",
                inv.NamedArgs.Select(kv => $"{kv.Key}={kv.Value}"));
            _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                $"Executing command: \"{inv.Command}\" (positional: [{posArgs}]; named: {{{namedArgs}}})");

            if (_router.TryExecute(inv, _output, out var execError))
            {
                _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                    new LogMessageBuilder()
                        .SetElapsedMs(cmdWatch.Elapsed.TotalMilliseconds)
                        .Build($"Command \"{inv.Command}\" executed successfully."));
            }
            else
            {
                _logger.Log(LogLevel.Debug, nameof(OrigoConsole),
                    new LogMessageBuilder()
                        .SetElapsedMs(cmdWatch.Elapsed.TotalMilliseconds)
                        .Build($"Command \"{inv.Command}\" failed: {execError}"));
                if (!string.IsNullOrEmpty(execError))
                    _output.Publish(execError);
            }
        }
    }
}
