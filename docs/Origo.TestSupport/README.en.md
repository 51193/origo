<!-- docsync-pair: Origo.TestSupport/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->

# Origo.TestSupport

> [↑ Back to Origo.manual](../README.en.md)

Shared test infrastructure for all test projects. Provides unified test doubles,
an in-memory file system, performance reporting utilities, and shared strategy
base classes.

## Modules

| Sub-module | Description |
|------------|-------------|
| [Architecture](Architecture/README.en.md) | `PrivateFieldNamingConvention` naming guard, TypedData test reset helper, and test-side frame-flush driver |
| [FileSystem](FileSystem/README.en.md) | Pure in-memory `IFileSystem` test double |
| [Logging](Logging/README.en.md) | `ILogger` in-memory log collector |
| [Node](Node/README.en.md) | `INodeHandle` / `INodeFactory` test doubles |
| [Observer](Observer/README.en.md) | Observer event collection infrastructure |
| [Reporting](Reporting/README.en.md) | Performance benchmark reporter |
| [Scene](Scene/README.en.md) | `ISndSceneHost` test doubles (including `StubSndSceneHost` / `StubSndEntity`) |
| [Snd](Snd/README.en.md) | `LevelBuilder` offline level construction tooling |
| [Strategies](Strategies/README.en.md) | Shared test strategy base classes and index constants |

## Usage

All test projects gain access via `InternalsVisibleTo`.
Core.Tests imports the namespace globally via `global using Origo.TestSupport;`.
