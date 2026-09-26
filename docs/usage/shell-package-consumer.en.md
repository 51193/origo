<!-- docsync-pair: usage/shell-package-consumer -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Shell-Only Package Consumption Verification

> [↑ Back to usage](README.en.md) · [↔ GodotAdapter Bootstrap](../Origo.GodotAdapter/Bootstrap/README.en.md) · [↔ shell/kernel boundary](../architecture/shell-kernel-boundary.en.md)

`scripts/package-consumer-smoke.sh` verifies two fresh consumers in an isolated temporary directory and a run-owned temporary NuGet package cache: one restores only the `Origo.Core` and `Origo.GodotAdapter` NuGet packages and starts through a real Godot `Node` entry; the other restores only `Origo.ConsoleBridge` and completes a command/output round-trip through the public bridge path. Neither contains an Origo project reference or test helper.

## Version Pins

| Item | Value | Authority |
|------|-------|-----------|
| Package version | Current repository version (for example `0.0.10-nightly.YYYYMMDD`) | `<Version>` in `Directory.Build.props` |
| .NET | `net10.0` | SDK pinned by `global.json` |
| Godot.NET.Sdk | `4.7.2` | `Origo.GodotAdapter.csproj`; the consumer fixture must match and the script compares both |
| Package references | `Origo.Core` + `Origo.GodotAdapter` | `tools/ShellPackageConsumer/OrigoShellPackageConsumer.csproj` |
| ConsoleBridge package reference | `Origo.ConsoleBridge` | `tools/ConsoleBridgePackageConsumer/OrigoConsoleBridgePackageConsumer.csproj` |

The consumer fixture is not part of `Origo.sln` and contains no `ProjectReference`; the script copies it to a temporary directory before restoring.

## Execution Flow

`bash scripts/package-consumer-smoke.sh`:

1. Packs `Origo.Core.Contracts`, `Origo.Core.Kernel`, `Origo.Core`, `Origo.GodotAdapter`, and `Origo.ConsoleBridge` into a fresh temporary local feed.
2. Checks that the `Origo.Core.Contracts` package contains the analyzer asset `analyzers/dotnet/cs/Origo.SourceGeneration.dll`; missing assets fail immediately. It then runs `scripts/validate-release-packages.sh` against the same temporary feed to verify the five package identities, versions, exact shell/kernel pairing, dependency direction, analyzer assets, and kernel isolation.
3. Copies the Core/Adapter fixture to a temporary directory outside the repository, switches consumer restore/build to a run-owned temporary `NUGET_PACKAGES` cache, writes a `nuget.config` with only the local feed and NuGet.org, and runs `dotnet restore`.
4. Verifies that `project.assets.json` resolves Core, Adapter, Contracts, and Kernel packages, and that the consumer sources contain no `<ProjectReference>`.
5. Builds the Core/Adapter consumer with `-warnaserror`; the kernel runtime assembly must appear in the build output.
6. Copies `KernelLeakProbe.cs.template` to `KernelLeakProbe.cs` and builds again; the build must fail with CS0246 because `Origo.Core.Runtime.OrigoRuntime` is unreachable from a shell-only consumer.
7. Copies `tools/ConsoleBridgePackageConsumer` to a separate temporary directory, references only the `Origo.ConsoleBridge` package, and restores/builds it with `-warnaserror`; `project.assets.json` must resolve `Origo.ConsoleBridge` and `Origo.Core.Contracts`, must not resolve `Origo.Core` or `Origo.Core.Kernel`, and the sources must contain no `<ProjectReference>`. The build output must not contain Core shell or kernel runtime assemblies.
8. Runs the ConsoleBridge consumer: it connects a real `TcpClient` over loopback, verifies that commands reach `IConsoleInputSource` and that `IConsoleOutputChannel` output arrives back at the client, and prints `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`.
9. Copies `tools/ConsoleBridgePackageConsumer/KernelLeakProbe.cs.template` and builds the ConsoleBridge consumer again; the build must fail with CS0246 because `Origo.Core.Runtime.OrigoRuntime` is unreachable, proving kernel compile assets do not leak through the ConsoleBridge package either.
10. Uses `scripts/download-godot.sh` to obtain Godot 4.7.2 and runs the Core/Adapter fixture headlessly; standard output must contain `SHELL_CONSUMER_STARTUP_OK kernel=True foreground=True`, and the process must exit with code 0.

## Consumer Entry

`tools/ShellPackageConsumer/ConsumerEntry.cs` derives from `OrigoDefaultEntry` and uses only stable shell APIs:

- Sets entry config, maps, initial save root, and save root before `base._Ready()`;
- Calls `default(TypedData).TryGetString(...)` to prove the Contracts package carries generated members;
- Drives the public startup pipeline through `Runtime.DriveFrame`;
- Checks that `Origo.Core.Kernel` loaded as a runtime assembly and that a foreground session exists;
- Calls `Context.Save.ListSaves()`, prints the startup marker, and exits.

`tools/ConsoleBridgePackageConsumer/Program.cs` references only the ConsoleBridge package, connects a real `TcpClient` over loopback, verifies command delivery into `IConsoleInputSource` and output delivery through `IConsoleOutputChannel`, and prints `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`.

## CI and Failure Semantics

- The normal CI `godot-integration-tests` job runs this script after the Godot integration tests; local `scripts/ci.sh` runs it last as well.
- Consumer restore/build uses a run-owned temporary NuGet package cache, and the script asserts that `project.assets.json` points at it; an ambient global package with the same Origo id/version cannot bypass the local feed and make the smoke validate stale artifacts.
- A missing package, a release package validator failure, a missing analyzer asset, a `ProjectReference` in either consumer, NuGet or compiler warnings, compilable kernel types, a missing Core-consumer kernel runtime assembly, unexpected Core/Kernel resolution or assemblies in the ConsoleBridge consumer, a failed ConsoleBridge round-trip or missing `CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK`, or a failed Godot startup or missing `SHELL_CONSUMER_STARTUP_OK` all fail the job.
- Fixture and script version pins are maintained by `scripts/package-consumer-smoke.sh` and `OrigoShellPackageConsumer.csproj`; a Godot SDK upgrade must update the adapter and consumer in the same change.

---
[↑ Back to Origo Manual](../README.en.md)
