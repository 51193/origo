<!-- docsync-pair: Origo.GodotAdapter/Logging/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Logging

> [↑ Back to Origo.GodotAdapter](../README.en.md) · [↔ Core abstraction: Abstractions/Logging](../../Origo.Core/Abstractions/Logging/README.en.md)

## Overview

The Godot implementation of the `ILogger` interface. Injects output delegates via the constructor to forward log messages to Godot engine's logging system (`GD.Print` / `GD.PushWarning` / `GD.PushError`). Supports minimum log level filtering.

## Files

| File | Responsibility |
|------|------|
| `GodotLogger.cs` | Godot logging implementation, delegate injection + level filtering |

## Implementation Details

```csharp
public sealed class GodotLogger : ILogger
{
    private readonly Action<LogLevel, string, string> _handler;
    private readonly LogLevel _minimumLevel;

    public GodotLogger(
        Action<LogLevel, string, string> handler,
        LogLevel minimumLevel = LogLevel.Info);

    public void Log(LogLevel level, string tag, string message)
    {
        if (level < _minimumLevel) return;
        _handler.Invoke(level, tag, message);
    }
}
```

Contains no static state. The actual output behavior (formatting, level routing) is controlled by external delegates. `minimumLevel` defaults to `Info`; `Debug` messages below this level do not trigger the delegate. `handler` cannot be null (validated by `ArgumentNullException.ThrowIfNull` at construction). Typical usage (from `OrigoAutoHost`):

```csharp
new GodotLogger((level, tag, message) =>
{
    switch (level)
    {
        case LogLevel.Warning: GD.PushWarning($"[{tag}] {message}"); break;
        case LogLevel.Error: GD.PushError($"[{tag}] {message}"); break;
        default: GD.Print($"[{tag}] {message}"); break;
    }
});
```

## Design Decisions

### Why use delegates instead of hardcoding GD.Print

Different usage scenarios may require different log routing (Godot editor console, file logging, remote logging). Delegate injection lets the caller decide the output strategy; `GodotLogger` itself is only responsible for interface adaptation.

### Why not format inside the Log method

Formatting responsibility (`[tag] message`) is left to the delegate implementation. Core layer's `LogMessageBuilder` already handles structured message construction. Duplicating formatting in `GodotLogger` would lead to inconsistent output styles.

### Why the default minimum level is Info rather than Debug

`Debug`-level logs are primarily used for detailed diagnostics during development (strategy instance creation/teardown, entity lifecycle hooks, etc.). In production (demo runs, release builds), these messages are high in volume and low in information density; they are off by default to avoid log flooding. When diagnostics are needed, explicitly pass `LogLevel.Debug`.

---

[↑ Back to Origo.GodotAdapter](../README.en.md)
