<!-- docsync-pair: Origo.GodotAdapter.Tests/Logging -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Logging Tests (Adapter Layer)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Logging](../Origo.GodotAdapter/Logging/README.en.md)

## Behavior Under Test Overview

Verifies GodotLogger's delegate injection pattern and level filtering: proxying log output via
`Action<LogLevel, string, string>` delegate, not throwing when handler is null, and minimum log level filtering.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `GodotLoggerTests.cs` | GodotLogger delegate injection, null handler safety, and level filtering |

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Log_WithHandler_InvokesHandlerWithCorrectLevelTagAndMessage` | Log(Warning, "Tag", "msg") → handler receives correct arguments | GodotAdapter Logging |
| `Log_WithNullHandler_DoesNotThrow` | No handler passed at construction, Log calls do not throw | GodotAdapter Logging |
| `Log_EachLogLevel_PassesCorrectLevel` | All four levels are passed correctly | GodotAdapter Logging |
| `Log_NullTagAndMessage_DoesNotThrow` | null tag and message do not throw | GodotAdapter Logging |

### Boundary Path (Level Filtering)

| Test Method | Boundary Condition | Expected Behavior |
|------------|-------------------|-------------------|
| `MinimumLevel_DefaultInfo_SuppressesDebug` | Default MinLevel=Info, Log(Debug) | Handler not invoked |
| `MinimumLevel_DefaultInfo_AllowsInfo` | Default MinLevel=Info, Log(Info) | Handler invoked |
| `MinimumLevel_ExplicitDebug_AllowsDebug` | MinLevel=Debug, Log(Debug) | Handler invoked |
| `MinimumLevel_Error_SuppressesWarning` | MinLevel=Error, Log(Warning) | Handler not invoked |
| `MinimumLevel_Error_AllowsError` | MinLevel=Error, Log(Error) | Handler invoked |

## Test Support Strategy

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| None | — | This test file defines no support strategies; collects callback parameters via captured closure variables and local `Action<LogLevel, string, string>` delegates |

## Known Coverage Gaps

| Gap Description | Impact | Doc Basis |
|----------------|--------|-----------|
| Path of `GodotLogger` output via real Godot `GD.Print` / `GD.PushWarning` / `GD.PushError` not covered (depends on Godot engine runtime) | Default delgate-less engine-level output behavior not directly verified in tests | Origo.GodotAdapter/Logging |
| Propagation/swallowing behavior of `GodotLogger.Log` when delegate throws not covered | Robustness under failing delegate not verified | Origo.GodotAdapter/Logging |

---

[↑ Back to Origo.GodotAdapter.Tests](README.en.md)
