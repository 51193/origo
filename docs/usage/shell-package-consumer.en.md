<!-- docsync-pair: usage/shell-package-consumer -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Shell-Only Package Consumption Verification

> [↑ Back to usage](README.en.md) · [↔ GodotAdapter Bootstrap](../Origo.GodotAdapter/Bootstrap/README.en.md) · [↔ shell/kernel boundary](../architecture/shell-kernel-boundary.en.md)

`scripts/package-consumer-smoke.sh` verifies in an isolated temporary directory and a run-owned temporary NuGet package cache that a fresh consumer restores only the `Origo.Core` and `Origo.GodotAdapter` NuGet packages — with no Origo project reference or test helper — compiles, and starts through a real Godot `Node` entry.

## Version Pins

| Item | Value | Authority |
|------|-------|-----------|
| Package version | Current repository version (for example `0.0.10-nightly.YYYYMMDD`) | `<Version>` in `Directory.Build.props` |
| .NET | `net10.0` | SDK pinned by `global.json` |
| Godot.NET.Sdk | `4.7.2` | `Origo.GodotAdapter.csproj`; the consumer fixture must match and the script compares both |
| Package references | `Origo.Core` + `Origo.GodotAdapter` | `tools/ShellPackageConsumer/OrigoShellPackageConsumer.csproj` |

The consumer fixture is not part of `Origo.sln` and contains no `ProjectReference`; the script copies it to a temporary directory before restoring.

## Execution Flow

`bash scripts/package-consumer-smoke.sh`:

1. Packs `Origo.Core.Contracts`, `Origo.Core.Kernel`, `Origo.Core`, and `Origo.GodotAdapter` into a fresh temporary local feed.
2. Checks that the `Origo.Core.Contracts` package contains the analyzer asset `analyzers/dotnet/cs/Origo.SourceGeneration.dll`; missing assets fail immediately.
3. Copies the fixture to a temporary directory outside the repository, switches consumer restore/build to a run-owned temporary `NUGET_PACKAGES` cache, writes a `nuget.config` with only the local feed and NuGet.org, and runs `dotnet restore`.
4. Verifies that `project.assets.json` resolves Core, Adapter, Contracts, and Kernel packages, and that the consumer sources contain no `<ProjectReference>`.
5. Builds the consumer with `-warnaserror`; the kernel runtime assembly must appear in the build output.
6. Copies `KernelLeakProbe.cs.template` to `KernelLeakProbe.cs` and builds again; the build must fail with CS0246 because `Origo.Core.Runtime.OrigoRuntime` is unreachable from a shell-only consumer.
7. Uses `scripts/download-godot.sh` to obtain Godot 4.7.2 and runs the fixture headlessly; standard output must contain `SHELL_CONSUMER_STARTUP_OK kernel=True foreground=True`, and the process must exit with code 0.

## Consumer Entry

`tools/ShellPackageConsumer/ConsumerEntry.cs` derives from `OrigoDefaultEntry` and uses only stable shell APIs:

- Sets entry config, maps, initial save root, and save root before `base._Ready()`;
- Calls `default(TypedData).TryGetString(...)` to prove the Contracts package carries generated members;
- Drives the public startup pipeline through `Runtime.DriveFrame`;
- Checks that `Origo.Core.Kernel` loaded as a runtime assembly and that a foreground session exists;
- Calls `Context.Save.ListSaves()`, prints the startup marker, and exits.

## CI and Failure Semantics

- The normal CI `godot-integration-tests` job runs this script after the Godot integration tests; local `scripts/ci.sh` runs it last as well.
- Consumer restore/build uses a run-owned temporary NuGet package cache, and the script asserts that `project.assets.json` points at it; an ambient global package with the same Origo id/version cannot bypass the local feed and make the smoke validate stale artifacts.
- A missing package or analyzer asset, a consumer `ProjectReference`, NuGet or compiler warnings, compilable kernel types, a missing kernel runtime assembly, a failed Godot startup, or a missing startup marker all fail the job.
- Fixture and script version pins are maintained by `scripts/package-consumer-smoke.sh` and `OrigoShellPackageConsumer.csproj`; a Godot SDK upgrade must update the adapter and consumer in the same change.

---
[↑ Back to Origo Manual](../README.en.md)
