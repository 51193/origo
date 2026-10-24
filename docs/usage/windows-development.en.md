# Windows Development (PowerShell)

> [↑ Back to Usage Documentation](README.en.md)

Origo keeps one POSIX script set (`scripts/*.sh`) for Linux/macOS and one
Windows-native script set (`scripts/windows/*.ps1`). The two sets implement the
same gates; neither invokes the other. On Windows, use the PowerShell scripts
and do not call the Bash scripts from Git Bash.

## Prerequisites

- Windows 10/11 with Windows PowerShell 5.1 or later.
- [Git for Windows](https://git-scm.com/download/win) for `git` (history and
  tags are required by `api-inventory.ps1` and `lint-commits.ps1`). Git Bash is
  not required and the PowerShell suite never invokes it.
- Python 3 for `check.ps1`, `lint-scripts.ps1`, and the JavaScript-free
  documentation guards. The suite resolves `python3`, `python`, or `py -3`.
- Network access on first use for the .NET SDK, the Godot engine archive, and
  NuGet packages.

## First Run

Install the exact SDK pinned by `global.json` into `%USERPROFILE%\.dotnet`
(the repository-local `.dotnet\` is used when it already exists or the user
directory is unavailable):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\install-dotnet.ps1
```

Then run the full local gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\ci.ps1
```

## Command Reference

| Gate | Command (from the repository root) | Equivalent POSIX script |
|---|---|---|
| Install SDK | `scripts\windows\install-dotnet.ps1` | `scripts/install-dotnet.sh` |
| Format | `scripts\windows\format.ps1` | `scripts/format.sh` |
| Shell API baseline | `scripts\windows\api-inventory.ps1` | `scripts/api-inventory.sh` |
| Build + tests + coverage | `scripts\windows\test.ps1` | `scripts/test.sh` |
| Benchmarks | `scripts\windows\benchmark.ps1` | `scripts/benchmark.sh` |
| Godot integration | `scripts\windows\godot-test.ps1` | `scripts/godot-test.sh` |
| Package consumer smoke | `scripts\windows\package-consumer-smoke.ps1` | `scripts/package-consumer-smoke.sh` |
| Script lint | `scripts\windows\lint-scripts.ps1` | `scripts/lint-scripts.sh` |
| Layered checks | `scripts\windows\check.ps1 <plan\|quick\|affected\|full>` | `scripts/check.sh` |
| Commit lint | `scripts\windows\lint-commits.ps1` | `scripts/lint-commits.sh` |
| Full local pipeline | `scripts\windows\ci.ps1` | `scripts/ci.sh` |
| Godot download | `scripts\windows\download-godot.ps1` | `scripts/download-godot.sh` |

Examples:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\check.ps1 plan
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\check.ps1 quick -Project Origo.Core.Tests/Origo.Core.Tests.csproj -Filter 'FullyQualifiedName~Save'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\test.ps1
```

`check.ps1 full` runs `ci.ps1` and then `lint-commits.ps1`.

On hosts where Windows Smart App Control or Defender blocks or hangs the
unsigned VSTest host, run the tests through the xUnit in-process runner:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\test.ps1 -Direct
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\ci.ps1 -DirectTests
```

`-Direct` (`-DirectTests` for `ci.ps1`) runs every test project without
VSTest/Coverlet, so it verifies behavior but not the coverage threshold. The
default path and CI use `dotnet test` with Coverlet and the >=90% line gate.

## How the Windows Suite Works

`scripts/windows/common.ps1` centralizes the platform mapping:

- **SDK selection**: `DOTNET_INSTALL_DIR`, repository-local `.dotnet\`, then
  `%USERPROFILE%\.dotnet\`, then `dotnet` on `PATH`; each candidate is validated
  by running `dotnet --version` from the repository root, which applies
  `global.json` and its `rollForward` rule. The suite sets `DOTNET_ROOT`,
  `DOTNET_CLI_HOME`, and disables MSBuild node reuse/compiler-server handles so
  redirected process output cannot deadlock.
- **Python selection**: runs `python3`, `python`, or `py -3` and keeps the first
  interpreter that can execute `import sys`; a Microsoft Store alias that only
  exists on `PATH` is rejected. Python runs in UTF-8 mode so shared helpers
  decode subprocess output consistently.
- **Godot**: reads the `Godot.NET.Sdk` pin from the adapter project, caches the
  matching `win64` mono archive under `.godot_binary\<version>\`, and redirects
  `APPDATA`/`LOCALAPPDATA` to `.godot-home\` so headless tests never write real
  user data.
- **Native paths**: all paths are native Windows paths; there is no
  `cygpath`/MSYS path conversion in this suite.

`package-consumer-smoke.ps1` validates the five-package graph directly from the
`.nupkg` nuspec files, restores the tracked consumer fixtures through a local
NuGet feed, checks the isolated package cache, runs the ConsoleBridge and Godot
public entry paths, and verifies that kernel compile assets are not reachable
from the shell packages. No Bash or shell pipeline is involved.

## CI and Platform Parity

`.github/workflows/ci.yml` runs the PowerShell suite in a dedicated
`windows-powershell` job with `fetch-depth: 0` (the API baseline selects the
previous formal release tag). The `build-and-test (windows-latest)` matrix job
also runs the POSIX script set, so both implementations are verified on
Windows.

The Windows benchmark comparison follows the same machine-capture rule as the
POSIX gate: thresholds apply only when `docs/benchmarks/baseline.json` was
captured on the current machine; otherwise the benchmark run is a smoke test.

If Windows Smart App Control blocks the unsigned xUnit test host, use a normal
developer shell or turn Smart App Control off for the machine; this is a host
security policy, not a repository configuration. The test command itself is the
standard `dotnet test`.
