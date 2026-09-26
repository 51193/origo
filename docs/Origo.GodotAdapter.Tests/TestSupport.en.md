<!-- docsync-pair: Origo.GodotAdapter.Tests/TestSupport -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# TestSupport

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)

## Behavior Under Test Overview

The `TestSupport/` directory provides test infrastructure (test doubles and static factories), containing
no `[Fact]` / `[Theory]` test methods itself. It is reused by Console, Architecture, and other capability
tests to set up an `OrigoRuntime` / session environment without the Godot engine runtime.

Common test doubles (`TestLogger`, `PerfReporter`, `TestMemoryFileSystem`, `TestSndSceneHost`
with `DummySndEntity`, etc.) have been extracted to the shared project
[Origo.TestSupport](../Origo.TestSupport/README.en.md). GodotAdapter.Tests reuses them via project reference.

## Test Support Facilities

| Facility | Type | Purpose |
|----------|------|---------|
| `NullFileSystem` | `IFileSystem` implementation | Minimal file system stand-in: all I/O operations throw `NotSupportedException`, path operations return basic concatenation, enumeration returns empty |
| `TestSndSceneHost` | `ISndSceneHost` implementation | In-memory scene host, maintains entity dictionary, supports lookup by name, `AddEntity`, `RemoveEntity`, and `RequestKillEntity` (duplicate kill throws). **GodotAdapter-specific** — differs from the shared version (`CreateEntity` throws, includes `AddEntity` injection method) |
| `InMemorySndEntity` | `ISndEntity` implementation | In-memory entity stand-in, dictionary-based `SetData` / `GetData` / `TryGetData`, strategy / node / observer methods are no-ops. **GodotAdapter-specific** — `InvokeStrategy` returns null (shared `DummySndEntity` throws) |
| `TestRuntimeHelper` | Static factory class | `CreateRuntime()` quickly creates `OrigoRuntime` + `TestSndSceneHost` (built-in `NullFileSystem`); `BootstrapForegroundSession()` loads main menu entry save via in-memory file system and flushes deferred queue |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| None | These are test infrastructure (no `[Fact]`), their behavior is indirectly verified by the capability tests (Console / Architecture) that reuse them; no separate coverage targets | — |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
