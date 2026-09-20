<!-- docsync-pair: architecture/shell-kernel-boundary -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Stable Shell/Kernel Boundary

> [↑ Back to architecture](README.en.md)

This document defines Origo's stable shell, kernel, and consumer compatibility
boundary for 0.1.0 and records the implementation, verification, and release
plan. It corresponds to
[issue #34](https://github.com/51193/origo/issues/34).

## Purpose and scope

Consumer contracts and implementation details no longer share one public
package surface. The stable boundary must:

- let game developers and agents compile and run by referencing shell packages
  only;
- let kernel implementations evolve independently, with kernel compile assets
  excluded from the consumer compilation surface;
- provide verifiable compatibility for lifecycle ordering, observer recovery,
  persistence semantics, and fail-fast behavior;
- preserve single access paths, interface segregation, adapter isolation, and
  stateless strategy rules;
- give the runnable consumer demo, machine API inventory, and consumer
  documentation a stable target surface.

The boundary does not freeze every currently exported type, expose kernel
implementation types, or allow compatibility layers to bypass orchestration,
validation, hooks, or resource lifecycle. ConsoleBridge JSON Lines,
third-party TypedData registration, and new gameplay capabilities are separate
follow-up work.

## Package and dependency topology

The stable boundary consists of:

| Package | Role | Contents |
|---------|------|----------|
| `Origo.Core.Contracts` | Stable contracts | Public interfaces, pure data types, metadata, strategy base classes, data-source contracts, and logging abstractions |
| `Origo.Core.Kernel` | Implementation | Runtime construction, SND internals, save and storage, data-source codecs, console routing, and kernel-shell ports |
| `Origo.Core` | Consumer shell | Host facade, wrapper types, and package entry point; its compilation surface is Contracts plus shell types |
| `Origo.GodotAdapter` | Godot shell | Godot `Node`-derived entries, adapter capability providers, and consumer extension points |
| `Origo.ConsoleBridge` | Console bridge shell | TCP console bridge server and options |

Dependencies point in one direction:

```text
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel ◄── runtime-only ── Origo.Core shell
        ▲
        │ private compile
Origo.GodotAdapter shell ──► GodotSharp

Origo.ConsoleBridge shell ──► Origo.Core.Contracts
```

The `Origo.Core` shell dependency on `Origo.Core.Kernel` supplies runtime assets
only. Restoring `Origo.Core` does not give consumers kernel compile assets.
`Origo.GodotAdapter` may use Core kernel's private compilation surface and call
its internal ports, but its public signatures use only Contracts and shell
types. `Origo.ConsoleBridge` references stable Core Contracts and shell
interfaces only.

### Adapter remains a single shell package

Adapter is a relatively fixed capability provider: Godot file system, logging,
serialization, node factory, and `Node`-derived entries. Its implementation
combines stable shell types with Core kernel ports and does not carry
independently evolving orchestration logic. 0.1.0 therefore keeps a single
`Origo.GodotAdapter` shell package and does not create
`Origo.GodotAdapter.Kernel`.

Adapter internals still stay out of the consumer compilation surface: non-shell
Godot bridge types, command handlers, and helpers remain internal. The public
types of `Origo.GodotAdapter` are real Godot types, so the Godot editor,
`.tscn` files, script class discovery, and serialization see them directly
without assembly forwarding.

If Adapter later gains an independent compatibility cycle, a replaceable
kernel, or orchestration shared by multiple adapters, introducing an Adapter
kernel is evaluated through this document's package-topology section.

## Stable consumer surface

`Origo.Core.Contracts` and the shell packages jointly provide these stable
capability groups:

- host and runtime: `IOrigoRuntime`, `ISndWorldAccess`, `IOrigoFrameDriver`,
  `OrigoMeta`;
- context and sessions: `ISndContext`, `ISessionManager`, `ISessionRun`,
  `ISndSceneReadAccess`;
- SND entities and strategies: `ISndEntity` and narrow interfaces, lifecycle,
  active, observer, state-machine, and planning strategy base classes,
  `StrategyIndexAttribute`, `ObserveDataAttribute`, and strategy extensions;
- data and metadata: `TypedData`, `SndMetaData`, node/strategy/data metadata,
  `SndMetaFluentBuilder`, `DataSourceNode`, and data-source contracts;
- persistence and files: `ISndSaveOperations`, `ISndLifecycleOperations`,
  `ISaveMetaContributor`, `ISndFileAccess`, `ISndArchiveFileAccess`,
  `ISndTemplateAccess`;
- blackboard, state machine, console, and logging abstractions;
- Godot entries: `OrigoAutoHost`, `OrigoDefaultEntry`, `SndEntityNodeExtensions`,
  `CommandHandlerBase`, `GodotFileSystem`, `GodotLogger`,
  `GodotJsonConverterRegistry`, `GodotPackedSceneNodeFactory`;
- ConsoleBridge: `ConsoleBridgeServer`, `ConsoleBridgeOptions`.

Concrete value types and interface members follow the public surface of
`Origo.Core.Contracts` and the shell assemblies. Every public type and member is
classified as a shell contract, tooling extension, kernel implementation, or
test-only before the contract is frozen. The current exports are listed in
[shell-api-classification](shell-api-classification.en.md); kernel
implementation and test-only types do not enter the shell compilation surface.

### TypedData registration scope

`TypedData` in 0.1.0 supports first-party Origo type registration only. Its
public surface provides typed reads and writes plus JSON round-tripping; internal
storage, Kind registration, and layered bridges remain inside the implementation
boundary. The third-party adapter registration contract is provided by separate
follow-up design and is not opened in the 0.1.0 shell.

### Persistence and storage scope

The shell exposes save operations and display metadata only: `ISndSaveOperations`,
`ISndLifecycleOperations`, `ISaveMetaContributor`, `SaveMetaBuildContext`, and
`SaveMetaDataEntry`. `ISaveStorageService`, `ISavePathPolicy`, save payloads,
`PersistentBlackboard`, and `SndContextParameters` are kernel implementation
types; consumers do not depend on concrete storage layout or payload structure.
Custom storage and path policies are designed separately in a later version.

## Compatibility promise

0.1.0 is the first formal shell release. Within 0.1.x:

- **Source compatibility**: consumer code continues to compile against stable
  shell APIs after recompilation.
- **Behavior compatibility**: lifecycle ordering, observer binding recovery,
  save/load semantics, persistence completion signals, and fail-fast errors
  remain unchanged.
- **Persistence compatibility**: save format is identified by
  `origo.format_version`; reading old saves, reading current saves, and
  corrupted-data failure semantics are explicitly tested.
- **Generated-code compatibility**: `TryGetXxx`, nullable annotations, Kind
  allocation, and ORIGOSG diagnostics remain stable.
- **SDK pairing**: 0.1.x supports .NET 10 and Godot.NET.Sdk 4.7.2; a wider range
  is opened after later verification.

0.1.x does not promise binary compatibility: consumers recompile after updating
shell packages. New consumer APIs, behavior breaks, and old API removals target
0.2.0. Kernel packages make no consumer compatibility promise, but kernel-shell
port contracts are governed by this document. Shell and kernel use exact version
pairing within 0.1.x so restore cannot silently combine untested versions.

Godot-generated nested signal types are generated public surface and are
included automatically in the API baseline; they are produced by the Godot
source generator and are not maintained item by item in human documentation.

## Kernel-shell port rules

Kernel may own internal ports called only by shell, subject to:

- ports live in the `Origo.Core.Kernel.Ports` namespace and remain internal;
- ports are exposed only to the `Origo.Core` and `Origo.GodotAdapter` shells
  through `InternalsVisibleTo`;
- ports call existing orchestration entry points and do not bypass validation,
  hooks, resource lifecycle, or state transitions;
- every port has a contract test, a reason to exist, and a removal condition;
- port types never appear in shell public signatures;
- shell does not duplicate kernel internals to simulate behavior.

Adapter initialization, scene-host binding, observer topology, and lifecycle
calls use these ports. `Origo.GodotAdapter` does not create a second
orchestration path.

## Meta-instruction exception

The stable boundary needs one bounded exception: `Origo.Core`,
`Origo.GodotAdapter`, and `Origo.ConsoleBridge` promise shell contract and
behavior compatibility within 0.1.x; kernel packages keep the early-development
rule of no compatibility burden. The exception covers shell contracts, port
contracts, and compatibility tests only. It does not relax single access paths,
fail-fast behavior, or architectural isolation.

Source and documentation describe current state and do not use `legacy`,
`since`, or `old` evolution markers. Every compatible shell API records an
owner and a next `0.y.0` removal condition in the contract baseline.

## Implementation plan

### Phase one: Contracts extraction and API baseline

1. Create `Origo.Core.Contracts` and move stable interfaces, pure data types,
   metadata, strategy base classes, and data-source contracts into it.
2. Build a Roslyn API inventory tool that emits a reproducible JSON shell API
   baseline.
3. Add compile probes: a shell-only consumer compiles, while referencing kernel
   types fails.
4. Update project references and test assemblies so existing Core tests pass
   under the new layering.

### Phase two: Core kernel and Core shell

1. Create `Origo.Core.Kernel` and move runtime construction, SND internals,
   persistence, storage, and codecs into it.
2. Implement `IOrigoRuntime`, `ISndWorldAccess`, and the shell host facade;
   concrete `OrigoRuntime`, `SndWorld`, `SndContext`, and `SndContextParameters`
   remain in kernel.
3. Establish the kernel-shell port namespace and contract tests.
4. `Origo.Core` shell uses Contracts as its main compilation surface and takes
   kernel as a runtime-only dependency.
5. Handle `InternalsVisibleTo`, Source Generator host assembly identity, and
   ORIGOSG007 diagnostics.

### Phase three: single GodotAdapter shell package

1. Keep `OrigoAutoHost`, `OrigoDefaultEntry`, and the other public Godot types
   in the `Origo.GodotAdapter` shell assembly with real `Node` type identity.
2. Adapter uses Core kernel ports for scene-host, observer-topology, and
   lifecycle binding.
3. Non-shell Godot implementation stays internal; public signatures use only
   Contracts and shell types.
4. Verify entry discovery, `.tscn` references, script selection, exit cleanup,
   and headless startup in Godot.
5. Run the consumer smoke from a clean `PackageReference` environment.

### Phase four: ConsoleBridge and package validation

1. `Origo.ConsoleBridge` remains shell-only and references stable Core Contracts
   and shell interfaces.
2. Extension capabilities continue through `IConsoleInputSource`,
   `IConsoleOutputChannel`, and `IConsoleCommandHandler`; the bridge package
   does not add a second command path.
3. Validate `ref`/`lib`/analyzer assets, runtime dependencies, and kernel
   isolation for every shell package.
4. Add the local feed, consumer demo, and package smoke to CI.

### Phase five: compatibility gates and release

1. API diff fails when a shell public member is added, removed, or changed
   unless the baseline is updated in the same change.
2. Run the old-shell-contract against new-kernel contract-test matrix.
3. Run save-format golden tests, failure-semantics tests, and generated-code and
   diagnostic snapshots.
4. Update `AGENTS.md`, `docs/META.*`, and the release process with the bounded
   shell exception and packaging requirements.
5. Record the 0.1.0 boundary design and breaking changes in the Changelog, then
   publish 0.1.0 after the full CI, release verification, and commit-lint loop.

## Verification and gates

| Verification | Pass condition |
|--------------|----------------|
| Compilation boundary | A fresh shell-only consumer compiles; referencing kernel types fails |
| Behavior contracts | Lifecycle ordering, observer recovery, save/load, and fail-fast semantics remain unchanged |
| Package integrity | No kernel compile-asset leakage; shell runtime and analyzer assets are complete |
| Compatibility matrix | Old shell contracts pass contract tests on new kernels |
| Godot | Headless and editor verification covers Node entry discovery, startup, save recovery, and exit cleanup |
| API baseline | Unapproved shell API changes fail CI |
| Save format | `origo.format_version`, old/current save reads, and corruption failures pass golden tests |

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Compatibility layer bypasses orchestration and becomes a backdoor | Ports call only existing orchestration entry points and are reviewed as single access paths |
| Contracts grows and freezes implementation | Freeze only consumer paths; keep implementation detail in kernel |
| Adapter public surface expands | Maintain the internal boundary and API baseline with the single shell package |
| Source Generator host identity drifts | Fix the host assembly in the build and maintain diagnostic/generated snapshots |
| Shell and kernel versions drift | Use exact pairing in 0.1.x and run the compatibility matrix in CI |
| Godot entry discovery regresses | Run headless and editor entry tests on every Adapter change |

## Follow-up scope

Third-party TypedData adapter registration, analyzer-based API gates, binary
compatibility, custom storage and path extensions, and Adapter kernel
re-evaluation are separate work after 0.1.0. Their detailed context, acceptance
criteria, and implementation notes enter tracked design documents when that
work starts.

---
[↑ Back to architecture](README.en.md)
