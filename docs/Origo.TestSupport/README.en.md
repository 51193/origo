<!-- docsync-pair: docs/Origo.TestSupport/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Origo.TestSupport

Shared test infrastructure for all test projects. Provides unified test doubles,
an in-memory file system, performance reporting utilities, and shared strategy
base classes.

## Modules

- **`Logging/TestLogger`** — In-memory log collector with level filtering and message recording
- **`Reporting/PerfReporter`** — Performance benchmark reporter supporting single-method reports and comparisons
- **`FileSystem/TestMemoryFileSystem`** — In-memory `IFileSystem` implementation supporting full file system operations
- **`Node/TestNodeHandle`** — Test double for `INodeHandle`
- **`Node/TestNodeFactory`** — Test double for `INodeFactory`, supports simulated creation failures
- **`Scene/TestSndSceneHost`** — Test double for `ISndSceneHost` (includes `DummySndEntity`)
- **`Strategies/SharedTestStrategies`** — Shared abstract test strategy base classes
- **`Observer/TestObserverEvents`** — Observer event collection utilities

## Usage

All test projects gain access via `InternalsVisibleTo`.
Core.Tests imports the namespace globally via `global using Origo.TestSupport;`.
