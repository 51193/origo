<!-- docsync-pair: docs/Origo.ConsoleBridge.Tests/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.ConsoleBridge.Tests

> [↑ Back to Origo.manual](../README.en.md)
> [↔ Module under test: Origo.ConsoleBridge](../Origo.ConsoleBridge/README.en.md)

## Test Strategy Overview

The tests for Origo.ConsoleBridge verify the complete behavior of the TCP remote console bridge server:
server lifecycle (Start / Stop / Dispose), command input (client send → input queue FIFO),
console output (Publish → client receive), connection management (single-connection mode, disconnect / reconnect),
thread safety (no deadlock under concurrent publish + read), and Agent workflow integration (command-response round-trip).

All tests use real `TcpClient` connections for integration testing (no mocking), ensuring the bridge
server works in a real network environment.

## Capability Document Index

| Capability | Document | Verification Focus |
|------------|----------|-------------------|
| Bridge Server | [ConsoleBridgeServer.md](ConsoleBridgeServer.en.md) | Lifecycle / Input / Output / Connection management / Thread safety / Agent workflow |
| Architecture | [Architecture.md](Architecture.en.md) | Assembly dependency direction (no Godot / GodotAdapter dependency) |

---

[↑ Back to Origo.manual](../README.en.md)
