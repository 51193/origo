<!-- docsync-pair: Origo.GodotAdapter.Tests/Logging -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Logging Tests (Adapter Layer)

> [↑ Back to Origo.GodotAdapter.Tests](README.en.md)
> [↔ Module under test: Origo.GodotAdapter/Logging](../Origo.GodotAdapter/Logging/README.en.md)

## Behavior Under Test Overview

Verifies GodotLogger's delegate injection pattern and level filtering: proxying log output via
`Action<LogLevel, string, string>` delegate, rejecting null handler at construction (ArgumentNullException), and minimum log level filtering.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `GodotLoggerTests.cs` | GodotLogger delegate injection, null handler rejection (ArgumentNullException), and level filtering |

### Happy Path

| Test Method | Verified Behavior | Doc Reference |
|------------|-------------------|---------------|
| `Log_WithHandler_InvokesHandlerWithCorrectLevelTagAndMessage` | Log(Warning, "Tag", "msg") → handler receives correct arguments | GodotAdapter Logging |
| `Constructor_WithNullHandler_Throws` | No handler passed at construction | ArgumentNullException |
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
