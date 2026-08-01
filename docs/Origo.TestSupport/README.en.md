<!-- docsync-pair: Origo.TestSupport/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Origo.TestSupport

Shared test infrastructure for all test projects. Provides unified test doubles,
an in-memory file system, performance reporting utilities, and shared strategy
base classes.

## Modules

| Sub-module | Description |
|------------|-------------|
| [FileSystem](FileSystem/README.en.md) | Pure in-memory `IFileSystem` test double |
| [Logging](Logging/README.en.md) | `ILogger` in-memory log collector |
| [Node](Node/README.en.md) | `INodeHandle` / `INodeFactory` test doubles |
| [Observer](Observer/README.en.md) | Observer event collection infrastructure |
| [Reporting](Reporting/README.en.md) | Performance benchmark reporter |
| [Scene](Scene/README.en.md) | `ISndSceneHost` test double |
| [Strategies](Strategies/README.en.md) | Shared test strategy base classes and index constants |

## Usage

All test projects gain access via `InternalsVisibleTo`.
Core.Tests imports the namespace globally via `global using Origo.TestSupport;`.
