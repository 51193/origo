
# Origo.TestSupport

> [↑ Back to Origo Manual](../README.en.md)

Shared test infrastructure for all test projects. Provides unified test doubles,
an in-memory file system, performance reporting utilities, and shared strategy
base classes.

## Modules

| Sub-module | Description |
|------------|-------------|
| [Architecture](Architecture/README.en.md) | `PrivateFieldNamingConvention` naming guard, shell API classification completeness guard, TypedData test reset helper, and test-side frame-flush driver |
| [FileSystem](FileSystem/README.en.md) | Pure in-memory `IFileSystem` test double |
| [Logging](Logging/README.en.md) | `ILogger` in-memory log collector |
| `Metadata` | `TypedDataTestSupport` reset helper for the process-wide TypedData kind registry and layered registration chains; documented under [Architecture](Architecture/README.en.md) |
| [Node](Node/README.en.md) | `INodeHandle` / `INodeFactory` test doubles |
| [Observer](Observer/README.en.md) | Observer event collection infrastructure |
| [Reporting](Reporting/README.en.md) | Performance benchmark reporter |
| `Runtime` | `SndContextTestFrameDriver` deferred-queue frame-flush extension for SndContext workflow tests; documented under [Architecture](Architecture/README.en.md) |
| [Scene](Scene/README.en.md) | `ISndSceneHost` test doubles (including `StubSndSceneHost` / `StubSndEntity`) |
| [Snd](Snd/README.en.md) | `LevelBuilder` offline level construction tooling |
| [Strategies](Strategies/README.en.md) | Shared test strategy base classes and index constants |

## Usage

`Origo.Core.Tests` and `Origo.GodotAdapter.Tests` gain access to internal facilities via `InternalsVisibleTo`; public test-support types are consumed directly by every referencing test project.
Core.Tests imports the namespace globally via `global using Origo.TestSupport;`.
