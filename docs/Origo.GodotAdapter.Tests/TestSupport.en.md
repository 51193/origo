<!-- docsync-pair: Origo.GodotAdapter.Tests/TestSupport -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# TestSupport

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)

## Behavior Under Test Overview

The `TestSupport/` directory provides test infrastructure (test doubles and static factories), containing
no `[Fact]` / `[Theory]` test methods itself. It is reused by Console, Architecture, and other capability
tests to set up an `OrigoRuntime` / session environment without the Godot engine runtime.

## Test Support Facilities

| Facility | Type | Purpose |
|----------|------|---------|
| `NullFileSystem` | `IFileSystem` implementation | Minimal file system stand-in: all I/O operations throw `NotSupportedException`, path operations return basic concatenation, enumeration returns empty |
| `TestSndSceneHost` | `ISndSceneHost` implementation | In-memory scene host, maintains entity dictionary, supports lookup by name, `AddEntity`, `RemoveEntity`, and `RequestKillEntity` (duplicate kill throws) |
| `InMemorySndEntity` | `ISndEntity` implementation | In-memory entity stand-in, dictionary-based `SetData` / `GetData` / `TryGetData`, strategy / node / observer methods are no-ops |
| `TestLogger` | `ILogger` implementation | Collects logs by level into lists (Debugs / Infos / Warnings / Errors), entry format `[tag] message` |
| `PerfReporter` | `PerfReporter` | Performance comparison table outputter (`ReportTable` / `CompareTable`), writes to both console and xUnit test output, used by `GodotTypedDataPerformanceTests` |
| `TestRuntimeHelper` | Static factory class | `CreateRuntime()` quickly creates `OrigoRuntime` + `TestSndSceneHost` (built-in `NullFileSystem`); `BootstrapForegroundSession()` loads main menu entry save via in-memory file system and flushes deferred queue |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| None | These are test infrastructure (no `[Fact]`), their behavior is indirectly verified by the capability tests (Console / Architecture) that reuse them; no separate coverage targets | — |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
