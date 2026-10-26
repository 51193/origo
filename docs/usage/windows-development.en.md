# Windows Development (PowerShell)

> [↑ Back to Usage Documentation](README.en.md)

Origo keeps one POSIX script set (`scripts/bash/*.sh`) for Linux/macOS and one
Windows-native script set (`scripts/powershell/*.ps1`). The two sets implement the
same development gate sequence; neither invokes the other. Bash-free shared
Python validators run on both; Bash-backed POSIX self-tests remain Linux-side,
and the Windows suite is not a release gate. On Windows, use the PowerShell scripts
and do not call the Bash scripts from Git Bash.

## Prerequisites

- Windows 10/11 with Windows PowerShell 5.1 or later.
- [Git for Windows](https://git-scm.com/download/win) for `git` (history and
  tags are required by `api-inventory.ps1` and `lint-commits.ps1`). Git Bash is
  not required and the PowerShell suite never invokes it.
- Python 3 for `check.ps1`, `lint-scripts.ps1`, and the shared validators. The
  suite resolves `python3`, `python`, or `py -3`, and `lint-scripts.ps1` requires
  `PyYAML` (install with `python -m pip install pyyaml`).
- Network access on first use for the .NET SDK, the Godot engine archive, and
  NuGet packages.

## First Run

Install the exact SDK pinned by `global.json` into `%USERPROFILE%\.dotnet`
(the repository-local `.dotnet\` is used when it already exists or the user
directory is unavailable):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\powershell\install-dotnet.ps1
```

Then run the full local gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\powershell\ci.ps1
```

## Command Reference

| Gate | Command (from the repository root) | Equivalent POSIX script |
|---|---|---|
| Install SDK | `scripts\powershell\install-dotnet.ps1` | `scripts/bash/install-dotnet.sh` |
| Format | `scripts\powershell\format.ps1` | `scripts/bash/format.sh` |
| Shell API baseline | `scripts\powershell\api-inventory.ps1` | `scripts/bash/api-inventory.sh` |
| Build + tests + coverage | `scripts\powershell\test.ps1` | `scripts/bash/test.sh` |
| Benchmarks | `scripts\powershell\benchmark.ps1` | `scripts/bash/benchmark.sh` |
| Godot integration | `scripts\powershell\godot-test.ps1` | `scripts/bash/godot-test.sh` |
| Package consumer smoke | `scripts\powershell\package-consumer-smoke.ps1` | `scripts/bash/package-consumer-smoke.sh` |
| Script lint | `scripts\powershell\lint-scripts.ps1` | `scripts/bash/lint-scripts.sh` |
| Layered checks | `scripts\powershell\check.ps1 <plan\|quick\|affected\|full>` | `scripts/bash/check.sh` |
| Commit lint | `scripts\powershell\lint-commits.ps1` | `scripts/bash/lint-commits.sh` |
| Full local pipeline | `scripts\powershell\ci.ps1` | `scripts/bash/ci.sh` |
| Godot download | `scripts\powershell\download-godot.ps1` | `scripts/bash/download-godot.sh` |

Examples:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\powershell\check.ps1 plan
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\powershell\check.ps1 quick -Project Origo.Core.Tests/Origo.Core.Tests.csproj -Filter 'FullyQualifiedName~Save'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\powershell\test.ps1
```

`check.ps1 full` runs `ci.ps1` and then `lint-commits.ps1`.

## How the Windows Suite Works

`scripts/powershell/common.ps1` centralizes the platform mapping:

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
