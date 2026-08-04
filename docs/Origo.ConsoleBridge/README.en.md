<!-- docsync-pair: Origo.ConsoleBridge/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.ConsoleBridge

> [↑ Back to Origo.manual](../README.en.md) · [↔ Core: Runtime/Console](../Origo.Core/Runtime/Console/README.en.md)

## Overview

A TCP remote console bridge server. Allows connecting via telnet/nc (default port 9876) to remotely execute Origo console commands and receive output. Single-connection mode: only one client connection is allowed at a time.

## Files

| File | Responsibility |
|------|------|
| `ConsoleBridgeOptions.cs` | Configuration options: port number (default 9876) |
| `ConsoleBridgeServer.cs` | TCP console bridge: internal async I/O for accepting connections and reading commands |

## Architecture

```
telnet client ──TCP:9876──> ConsoleBridgeServer
                                ├── input:  ConsoleInputBuffer.Enqueue(line)
                                └── output: ConsoleOutputChannel.Subscribe(OnConsoleOutput)
                                              → StreamWriter.WriteLine
```

**Threading model**:
- **Async I/O**: `AcceptTcpClientAsync` and `ReadLineAsync` run on the ThreadPool without occupying dedicated threads. CancellationToken replaces `Monitor.Wait` polling and `ReceiveTimeout`, enabling immediate cancellation response.
- **Output path is synchronous**: `StreamWriter.WriteLine` in the `OnConsoleOutput` callback remains synchronous — console output involves short kernel calls, and the TCP send buffer will not fill up in practice; async would be a net loss.

## Usage

```bash
# Start the server (in Godot project code)
var server = new ConsoleBridgeServer(consoleInput, consoleOutput);
server.Start();

# Client connection
nc localhost 9876
> help
> spawn my_entity template_basic
> snd_count
```

## Security Boundary

> ⚠️ **No authentication, no encryption.** `ConsoleBridgeServer` is a plaintext
> TCP protocol: it does not verify client identity, does not encrypt commands
> or output, and single-connection mode is first-come-first-served (a client
> that holds the connection can execute any console command).

- **Intended use**: local (`localhost`) or trusted-LAN development/debugging,
  agent-driven development, and automated testing.
- **Forbidden**: exposing the port directly to the public internet or an
  untrusted network.
- **For remote access**: tunnel through existing secure channels such as SSH
  (`ssh -L 9876:localhost:9876 ...`), a VPN, or a reverse proxy; let the outer
  channel provide authentication and encryption.
- If a product needs authentication/encryption, it should be implemented as a
  separate security layer; this module keeps its debugging-oriented scope.

## Design Decisions

### Why single-connection mode

Origo's game frame loop is single-threaded. Multiple connections mean multiple command streams enter `ConsoleInputBuffer` concurrently, but command execution is serial within a frame — in a multi-client scenario, the order of command execution is indeterminate, leading to unpredictable results.

### Why output uses publish-subscribe instead of writing directly to the socket

`ConsoleBridgeServer` subscribes to `IConsoleOutputChannel`, so all logs and console output (not just command output from the bridge-connected client) are pushed to the connection. This lets the remote operator see the full game log stream for easier debugging.

### Why async I/O instead of manual threading

`AcceptTcpClientAsync` and `ReadLineAsync` wait at the OS level via IOCP without occupying dedicated thread stacks. 

- Zero CPU overhead when idle (no 100ms wake-up to check state)
- `Dispose()` shutdown latency reduced from worst-case 6 seconds (two `Thread.Join(3000)`) to sub-second (`CancellationToken` immediately interrupts async I/O)
- No `ReceiveTimeout` read timeout fallback is needed — cancellation tokens directly interrupt `ReadLineAsync`

The output path keeps synchronous `StreamWriter.WriteLine` to avoid changing the `Action<string>` callback to `Func<string, Task>` (which would cascade into polluting the `IConsoleOutputChannel` interface).

### Exception propagation strategy (fail-fast)

`ConsoleBridgeServer` does not swallow I/O exceptions for silent degradation, but distinguishes fault levels: connection-level exceptions (client disconnect, `ReadLineAsync` I/O errors) are isolated and logged as `Warning` (with the full stack trace) before the server continues accepting; a non-cancellation system-level socket error in the accept loop is logged as `Error` and stops the listener (to be restarted by the host process); `Dispose` checks the accept task state and logs an `Error` instead of swallowing a fault. A failed `Start` (e.g. port in use) rolls back its internal state, so the same instance can be retried once the cause is resolved.

### Output buffer overflow

When no client is connected, console output is buffered in an in-memory queue (max 1000 lines). When the limit is exceeded, the oldest lines are dropped, and a warning message (`[ConsoleBridge] Warning: N output line(s) were dropped due to buffer overflow.`) is prepended to the output stream when the client next connects, ensuring data loss is observable.

---
[↑ Back to Origo.manual](../README.en.md)
