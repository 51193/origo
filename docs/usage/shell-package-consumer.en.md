# Shell-Only Package Consumption Verification

> [↑ Back to usage](README.en.md) · [↔ GodotAdapter Bootstrap](../Origo.GodotAdapter/Bootstrap/README.en.md) · [↔ shell/kernel boundary](../architecture/shell-kernel-boundary.en.md)

`scripts/bash/package-consumer-smoke.sh` verifies three fresh consumers in isolated temporary directories with a run-owned temporary NuGet package cache: a Core-only console consumer that restores only the `Origo.Core` package and starts `OrigoHost` without a Godot engine, one that restores only the `Origo.Core` and `Origo.GodotAdapter` packages and starts through a real Godot `Node` entry, and one that restores only `Origo.ConsoleBridge` and completes a command/output round-trip through the public bridge path. None contains an Origo project reference or test helper.

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
13. Uses `scripts/bash/download-godot.sh` to obtain the Godot engine pinned by `Origo.GodotAdapter.csproj` and runs the Core/Adapter fixture headlessly; standard output must contain `SHELL_CONSUMER_STARTUP_OK kernel=True foreground=True`, and the process must exit with code 0.

## Consumer Entry

`tools/CorePackageConsumer/Program.cs` references only the `Origo.Core` package. It creates a temporary workspace with a minimal entry config and SND scene, implements the public `IFileSystem` contract over `System.IO`, starts `OrigoHost` through `OrigoHost.Create`, calls `Bootstrap()`, drives a frame, verifies that the deferred main-menu entry load created a foreground session, calls `Context.Save.ListSaves()`, enqueues a business deferred action and verifies it runs at the frame boundary, prints `CORE_CONSUMER_STARTUP_OK`, and disposes the host. It never references Godot, the adapter, ConsoleBridge, kernel types, or test helpers; the kernel leak probe proves the compile restriction.

`tools/ShellPackageConsumer/ConsumerEntry.cs` derives from `OrigoDefaultEntry` and uses only stable shell APIs:

- Sets entry config, maps, initial save root, and save root before `base._Ready()`;
- Calls `default(TypedData).TryGetString(...)` to prove the Contracts package carries generated members;
- Drives the public startup pipeline through `Runtime.DriveFrame`;
- Checks that `Origo.Core.Kernel` loaded as a runtime assembly and that a foreground session exists;
- Calls `Context.Save.ListSaves()`, prints the startup marker, and exits.

`tools/ConsoleBridgePackageConsumer/Program.cs` references only the ConsoleBridge package, connects a real `TcpClient` over loopback, verifies command delivery into `IConsoleInputSource` and output delivery through `IConsoleOutputChannel`, and prints `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`.

## CI and Failure Semantics

- The normal CI `godot-integration-tests` job runs this script after the Godot integration tests; local `scripts/bash/ci.sh` runs it last as well.
- Consumer restore/build uses a run-owned temporary NuGet package cache, and the script asserts that `project.assets.json` points at it; an ambient global package with the same Origo id/version cannot bypass the local feed and make the smoke validate stale artifacts.
- A missing package, a release package validator failure, a missing analyzer asset, a `ProjectReference` in any consumer, unexpected adapter, ConsoleBridge, or GodotSharp resolution in the Core-only consumer, NuGet or compiler warnings, compilable kernel types, a missing Core-consumer kernel runtime assembly, a failed Core-only host startup, deferred frame flush, or missing `CORE_CONSUMER_STARTUP_OK`, unexpected Core/Kernel resolution or assemblies in the ConsoleBridge consumer, a failed ConsoleBridge round-trip or missing `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`, a missing `unzip` while the engine cache is cold, or a failed Godot startup or missing `SHELL_CONSUMER_STARTUP_OK` all fail the job.
- The Godot SDK pin is parsed by `scripts/bash/download-godot.sh` alone, with the adapter csproj as the authority; the smoke and the integration tests compare their pins against it, so a Godot SDK upgrade must update the adapter and the consumer fixtures in the same change.

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
