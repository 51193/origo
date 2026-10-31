# Shell-Only Package Consumption Verification

> [↑ Back to usage](README.en.md) · [↔ GodotAdapter Bootstrap](../Origo.GodotAdapter/Bootstrap/README.en.md) · [↔ shell/kernel boundary](../architecture/shell-kernel-boundary.en.md)

`scripts/bash/package-consumer-smoke.sh` verifies three fresh consumers in isolated temporary directories with a run-owned temporary NuGet package cache: a Core-only console consumer that restores only the `Origo.Core` package and starts `OrigoHost` without a Godot engine, one that restores only the `Origo.Core` and `Origo.GodotAdapter` packages and starts through a real Godot `Node` entry, and one that restores only `Origo.ConsoleBridge` and completes a command/output round-trip through the public bridge path. None contains an Origo project reference or test helper.

## Scope and Boundary

This smoke is the **package adoption contract** for external projects. It
answers: can a fresh project restore the published NuGet packages, get the
expected dependency/assets graph, compile against the public shell surface,
start the supported entry point, and stay isolated from kernel compile assets?
It uses startup markers and minimal public-surface checks, not engine-behavior assertions; the Core/Adapter fixture additionally exercises a demo-like authored level, custom strategy discovery, node mapping, and save/load so the documented demo path is verified.

Behavior inside a real engine is owned by
[Origo.GodotAdapter.Integration.Tests](../Origo.GodotAdapter.Integration.Tests/README.en.md):
that suite runs against repository project references and verifies command,
file-system, lifecycle, bootstrap, and save/restore behavior. The CI Godot job
runs the integration suite first and this smoke second; a failure here is a
packaging/consumer-adoption failure, while a failure there is a behavior
failure. Keep the two boundaries separate; do not grow this smoke into a
behavior suite and do not use the integration suite to validate packaging.

`scripts/bash/package-consumer-smoke.sh` is the Linux/CI implementation.
`scripts/powershell/package-consumer-smoke.ps1` mirrors the same three
consumers (Core-only, Core+Adapter/Godot, ConsoleBridge) for Windows.

## Version Pins

| Item | Value | Authority |
|------|-------|-----------|
| Package version | Current source version (for example `0.1.1-dev` on `main` or `0.1.1` on a release tag) | `<Version>` in `Directory.Build.props` |
| .NET | `net10.0` | SDK pinned by `global.json` |
| Godot.NET.Sdk | `4.7.2` | `Origo.GodotAdapter.csproj`; the Core/Adapter fixture must match and the script compares both |
| Core-only package reference | `Origo.Core` | `tools/CorePackageConsumer/OrigoCorePackageConsumer.csproj` |
| Package references | `Origo.Core` + `Origo.GodotAdapter` | `tools/ShellPackageConsumer/OrigoShellPackageConsumer.csproj` |
| ConsoleBridge package reference | `Origo.ConsoleBridge` | `tools/ConsoleBridgePackageConsumer/OrigoConsoleBridgePackageConsumer.csproj` |

The consumer fixtures are not part of `Origo.sln` and contain no `ProjectReference`; the script copies each to a temporary directory before restoring.

## Execution Flow

`bash scripts/bash/package-consumer-smoke.sh`:

1. Packs `Origo.Core.Contracts`, `Origo.Core.Kernel`, `Origo.Core`, `Origo.GodotAdapter`, and `Origo.ConsoleBridge` into a fresh temporary local feed.
2. Runs `scripts/bash/validate-release-packages.sh` against the same temporary feed; the checks that validator owns are listed under [release pipeline artifacts](../release-process.en.md#release-pipeline-artifacts).
3. Copies the Core/Adapter fixture to a temporary directory outside the repository, switches consumer restore/build to a run-owned temporary `NUGET_PACKAGES` cache, writes a `nuget.config` with only the local feed and NuGet.org, and runs `dotnet restore`.
4. Verifies that `project.assets.json` resolves Core, Adapter, Contracts, and Kernel packages, and that the consumer sources contain no `<ProjectReference>`.
5. Builds the Core/Adapter consumer with `-warnaserror`; the kernel runtime assembly must appear in the build output.
6. Copies `KernelLeakProbe.cs.template` to `KernelLeakProbe.cs` and builds again; the build must fail with CS0246 because `Origo.Core.Runtime.OrigoRuntime` is unreachable from a shell-only consumer.
7. Copies `tools/CorePackageConsumer` to a separate temporary directory, restores only the `Origo.Core` package through the same isolated cache, and verifies that `project.assets.json` resolves `Origo.Core`, `Origo.Core.Contracts`, and `Origo.Core.Kernel` but not `Origo.GodotAdapter`, `Origo.ConsoleBridge`, or `GodotSharp`; the fixture source must contain no `<ProjectReference>`.
8. Builds the Core-only consumer with `-warnaserror` and runs it with `dotnet run`; the console process must print `CORE_CONSUMER_STARTUP_OK` without a Godot engine.
9. Copies `tools/CorePackageConsumer/KernelLeakProbe.cs.template` and builds the Core-only consumer again; the build must fail with CS0246 because kernel compile assets do not flow through the `Origo.Core` package.
10. Copies `tools/ConsoleBridgePackageConsumer` to a separate temporary directory, references only the `Origo.ConsoleBridge` package, and restores/builds it with `-warnaserror`; `project.assets.json` must resolve `Origo.ConsoleBridge` and `Origo.Core.Contracts`, must not resolve `Origo.Core` or `Origo.Core.Kernel`, and the sources must contain no `<ProjectReference>`. The build output must not contain Core shell or kernel runtime assemblies.
11. Runs the ConsoleBridge consumer: it connects a real `TcpClient` over loopback, verifies that commands reach `IConsoleInputSource` and that `IConsoleOutputChannel` output arrives back at the client, and prints `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`.
12. Copies `tools/ConsoleBridgePackageConsumer/KernelLeakProbe.cs.template` and builds the ConsoleBridge consumer again; the build must fail with CS0246 because `Origo.Core.Runtime.OrigoRuntime` is unreachable, proving kernel compile assets do not leak through the ConsoleBridge package either.
13. Uses `scripts/bash/download-godot.sh` to obtain the Godot engine pinned by `Origo.GodotAdapter.csproj` and runs the Core/Adapter fixture headlessly. The fixture boots through `OrigoDefaultEntry`, discovers its consumer-authored `DemoStrategy`, resolves the authored entity/node from the strategy-facing entity, verifies the authored `hp` and hook data, saves/loads the slot, calls `Context.Shutdown()`, and prints `DEMO_CONSUMER_OK` plus `SHELL_CONSUMER_STARTUP_OK kernel=True foreground=True`; the process must exit 0.

## Consumer Entry

`tools/CorePackageConsumer/Program.cs` references only the `Origo.Core` package. It creates a temporary workspace with a minimal entry config and SND scene, implements the public `IFileSystem` contract over `System.IO`, starts `OrigoHost` through `OrigoHost.Create`, calls `Bootstrap()`, drives a frame, verifies that the deferred main-menu entry load created a foreground session, calls `Context.Save.ListSaves()`, enqueues a business deferred action and verifies it runs at the frame boundary, prints `CORE_CONSUMER_STARTUP_OK`, and disposes the host. It never references Godot, the adapter, ConsoleBridge, kernel types, or test helpers; the kernel leak probe proves the compile restriction.

`tools/ShellPackageConsumer/ConsumerEntry.cs` derives from `OrigoDefaultEntry` and uses only stable shell APIs:

- Sets entry config, maps, initial save root, and save root before `base._Ready()`;
- Calls `default(TypedData).TryGetString(...)` to prove the Contracts package carries generated members;
- Lets the default auto-discovery register the consumer-authored `DemoStrategy`, drives the public startup pipeline through `Runtime.DriveFrame`, and verifies the configured non-default `main_menu_level` session, the authored entity/node, and the demo strategy data;
- Resolves the authored node inside `DemoStrategy` through `entity.GetNodeFromSnd<Node2D>("main")` from `Origo.GodotAdapter`, proving the strategy-facing inner entity path;
- Saves the state through `Context.Save.RequestSaveGame`, mutates data, loads it back through `RequestLoadGame`, and verifies the restored entity and hook data;
- Calls `Context.Shutdown()`, prints `DEMO_CONSUMER_OK` and the startup marker, and exits.

`tools/ConsoleBridgePackageConsumer/Program.cs` references only the ConsoleBridge package, connects a real `TcpClient` over loopback, verifies command delivery into `IConsoleInputSource` and output delivery through `IConsoleOutputChannel`, and prints `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`.

## CI and Failure Semantics

- The normal CI `godot-integration-tests` job runs this script after the Godot integration tests; local `scripts/bash/ci.sh` runs it last as well. The Windows PowerShell mirror is `scripts/powershell/package-consumer-smoke.ps1`.
- Consumer restore/build uses a run-owned temporary NuGet package cache, and the script asserts that `project.assets.json` points at it; an ambient global package with the same Origo id/version cannot bypass the local feed and make the smoke validate stale artifacts.
- A missing package, a release package validator failure, a missing analyzer asset, a `ProjectReference` in any consumer, unexpected adapter, ConsoleBridge, or GodotSharp resolution in the Core-only consumer, NuGet or compiler warnings, compilable kernel types, a missing Core-consumer kernel runtime assembly, a failed Core-only host startup, deferred frame flush, or missing `CORE_CONSUMER_STARTUP_OK`, unexpected Core/Kernel resolution or assemblies in the ConsoleBridge consumer, a failed ConsoleBridge round-trip or missing `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`, a missing `unzip` while the engine cache is cold, or a failed Godot startup, missing `DEMO_CONSUMER_OK` (authored level/strategy/node/save/load scenario), or missing `SHELL_CONSUMER_STARTUP_OK` all fail the job.
- The Godot SDK pin is parsed by `scripts/bash/download-godot.sh` alone, with the adapter csproj as the authority; the smoke and the integration tests compare their pins against it, so a Godot SDK upgrade must update the adapter and the consumer fixtures in the same change.

## Minimal Demo Capability Path

The Core/Adapter fixture is the reference for a package-only demo:

1. **Entry config** — `Origo/entry/entry.json` declares `levels` and the
   `main_menu_level`; the configured ID is the mounted foreground session ID.
2. **Authored level** — the level's `snd_scene.json` is an entity metadata
   array. Each entity names its Godot scene under `node.pairs`, its lifecycle
   strategy IDs under `strategy.lifecycle_indices`, and its initial data under
   `data.pairs` (for example `hp: Int32 = 100`).
3. **Consumer strategy** — a `sealed`, stateless `[StrategyIndex]` strategy in
   the consumer assembly is discovered at bootstrap (or registered manually via
   `ConfigureStrategies` with `AutoDiscoverStrategies = false`). Hooks such as
   `AfterSpawn`, `AfterLoad`, and `BeforeSave` run through Core.
4. **Presentation access** — from a strategy, use
   `entity.GetNode("main").GetNativeNode()` or
   `entity.GetNodeFromSnd<Node2D>("main")` (namespace `Origo.GodotAdapter`);
   null-check because background sessions have no Godot handle.
5. **Save/load** — `Context.Save.RequestSaveGame(id)`, pump frames until
   `Context.Deferred.IsPersistenceIdle`, `Context.Save.ListSaves()`, mutate
   data, then `Context.Save.RequestLoadGame(id)` and pump again. Restored
   entities re-run `AfterLoad`; the foreground session may be replaced.

The fixture keeps these checks minimal: it verifies adoption and the public
surface, while full behavior coverage stays in the integration and unit suites.
A complete runnable game demo is still tracked separately (#33).

### Secondary Levels and New Games

`entry.json` resolves only the configured main-menu level at startup. For a
second authored level, call `Context.Save.RequestSwitchForegroundLevel(levelId)`
and pump the frame until `IsPersistenceIdle`; when that level has no current
payload the switch mounts an empty session, so load the level's entity file
through `Context.Template.LoadMetaListFromFile(path)` and spawn it through
`ForegroundSession.SpawnMany(...)`. Alternatively start a new game from a
normal save slot (`RequestSaveGame`) rather than the packaged initial-save path,
which expects the repository's `save_000` / `default` layout. These are the
supported public composition points while first-class data-driven level loading
remains a tracked capability (#44/#33).

## Consuming a release from a consumer project

A formal release attaches the five shell/kernel packages plus `nuget.config`
(see [release pipeline artifacts](../release-process.en.md#release-pipeline-artifacts)).
Download the `.nupkg` files for the release tag you use into a local folder,
then make that folder the package source for your project:

- copy the attached `nuget.config` next to your project and point its
  `origo-local` source at the folder (the tracked
  [`nuget.config.example`](../../nuget.config.example) is the template), or
- add the feed to an existing `packageSources` entry, for example
  `<add key="origo-local" value="./packages/origo/" />`.

A console or server consumer that does not use Godot references only the Core
shell package and pins the release version:

```xml
<PackageReference Include="Origo.Core" Version="x.y.z" />
```

A Godot consumer adds the adapter package:

```xml
<PackageReference Include="Origo.Core" Version="x.y.z" />
<PackageReference Include="Origo.GodotAdapter" Version="x.y.z" />
```

`Origo.Core.Contracts` and `Origo.Core.Kernel` are restored as dependencies of
`Origo.Core` from the same local feed, and the adapter package supplies the
Godot runtime assets. Kernel compile assets stay out of the Core-only graph, so
the consumer starts through `OrigoHost` and the stable Contracts interfaces.
These are the package-only paths that the smoke test above verifies on every CI
run and release.

---
[↑ Back to Origo Manual](../README.en.md)
