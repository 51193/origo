# Common helpers for the Windows PowerShell development scripts.
# Dot-source from scripts/windows/*.ps1; keep this file compatible with
# Windows PowerShell 5.1 (no PS7-only syntax).

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:OrigoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:OrigoDotnetPath = $null
$script:OrigoPythonInfo = $null

function Get-OrigoRoot {
    return $script:OrigoRoot
}

function Write-OrigoHeader {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host ''
    Write-Host ('=' * 72)
    Write-Host $Message
    Write-Host ('=' * 72)
}

function Join-OrigoPath {
    param([Parameter(Mandatory = $true)][string[]]$Parts)
    $path = $Parts[0]
    for ($index = 1; $index -lt $Parts.Length; $index++) {
        $path = Join-Path $path $Parts[$index]
    }
    return $path
}

function Read-OrigoXml {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [xml](Get-Content -LiteralPath $Path -Raw)
}

function Get-OrigoBuildVersion {
    $props = Read-OrigoXml (Join-OrigoPath @((Get-OrigoRoot), 'Directory.Build.props'))
    foreach ($group in $props.Project.PropertyGroup) {
        if ($null -ne $group.Version -and -not [string]::IsNullOrWhiteSpace($group.Version)) {
            return [string]$group.Version
        }
    }
    throw 'Directory.Build.props has no <Version> value.'
}

function Get-OrigoVersionBase {
    param([Parameter(Mandatory = $true)][string]$Version)
    return ($Version -split '-', 2)[0]
}

function ConvertTo-OrigoVersion {
    param([Parameter(Mandatory = $true)][string]$Version)
    $base = Get-OrigoVersionBase $Version
    $parts = $base.Split('.')
    if ($parts.Length -ne 3) {
        throw "Version '$Version' is not x.y.z."
    }
    foreach ($part in $parts) {
        if ($part -notmatch '^[0-9]+$') {
            throw "Version '$Version' is not numeric."
        }
    }
    return [version]$base
}

function Get-OrigoGodotSdkVersion {
    $csproj = Join-OrigoPath @((Get-OrigoRoot), 'Origo.GodotAdapter', 'Origo.GodotAdapter.csproj')
    $text = Get-Content -LiteralPath $csproj -Raw
    $match = [regex]::Match($text, 'Godot\.NET\.Sdk/([0-9]+\.[0-9]+\.[0-9]+)')
    if (-not $match.Success) {
        throw "Could not parse Godot.NET.Sdk version from $csproj."
    }
    return $match.Groups[1].Value
}

function Initialize-OrigoDotnetEnvironment {
    param([Parameter(Mandatory = $true)][string]$DotnetPath)
    $sdkRoot = Split-Path -Parent $DotnetPath
    $cliHome = Join-OrigoPath @((Get-OrigoRoot), '.dotnet-cli-home')
    New-Item -ItemType Directory -Force -Path $cliHome | Out-Null
    $env:DOTNET_CLI_HOME = $cliHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    # Node reuse/compiler-server children keep the redirected stdout handles
    # open after the parent exits, which deadlocks Start-Process capture.
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
    if ($sdkRoot -ne '') {
        $env:DOTNET_ROOT = $sdkRoot
        $env:PATH = $sdkRoot + ';' + $env:PATH
    }
}

function Resolve-OrigoDotnet {
    if ($null -ne $script:OrigoDotnetPath) {
        return $script:OrigoDotnetPath
    }

    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_INSTALL_DIR)) {
        $candidates.Add((Join-OrigoPath @($env:DOTNET_INSTALL_DIR, 'dotnet.exe')))
    }
    $candidates.Add((Join-OrigoPath @((Get-OrigoRoot), '.dotnet', 'dotnet.exe')))
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $candidates.Add((Join-OrigoPath @($env:USERPROFILE, '.dotnet', 'dotnet.exe')))
    }
    $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $candidates.Add($command.Source)
    }

    $failures = @()
    foreach ($candidate in $candidates) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            continue
        }
        try {
            $probe = Invoke-OrigoNative -FilePath $candidate -Arguments @('--version') -AllowFailure
            if ($probe.ExitCode -eq 0 -and $probe.Output -match '[0-9]+\.[0-9]+\.[0-9]+') {
                Initialize-OrigoDotnetEnvironment $candidate
                $script:OrigoDotnetPath = $candidate
                return $candidate
            }
            $failures += ($candidate + ': ' + $probe.Output.Trim())
        }
        catch {
            $failures += ($candidate + ': ' + $_.Exception.Message)
        }
    }

    $detail = ($failures -join [Environment]::NewLine)
    $message = 'No .NET SDK satisfies global.json. Install it with scripts/windows/install-dotnet.ps1.' + [Environment]::NewLine + $detail
    throw $message
}

function Invoke-OrigoDotnet {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$WorkingDirectory = (Get-OrigoRoot),
        [switch]$AllowFailure,
        [switch]$Quiet,
        [switch]$PassThru
    )
    $dotnet = Resolve-OrigoDotnet
    $result = Invoke-OrigoNative -FilePath $dotnet -Arguments $Arguments -WorkingDirectory $WorkingDirectory -AllowFailure:$AllowFailure
    if (-not $Quiet -and -not [string]::IsNullOrWhiteSpace($result.Output)) {
        Write-Host $result.Output.TrimEnd()
    }
    if (-not $AllowFailure -and $result.ExitCode -ne 0) {
        throw ("dotnet " + ($Arguments -join ' ') + " failed with exit code " + $result.ExitCode)
    }
    if ($PassThru) {
        return $result
    }
}

function Invoke-OrigoDotnetStreaming {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$WorkingDirectory = (Get-OrigoRoot)
    )
    $dotnet = Resolve-OrigoDotnet
    Push-Location $WorkingDirectory
    try {
        & $dotnet @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($exitCode -ne 0) {
        throw ("dotnet " + ($Arguments -join ' ') + " failed with exit code " + $exitCode)
    }
}

function Get-OrigoPythonInfo {
    if ($null -ne $script:OrigoPythonInfo) {
        return $script:OrigoPythonInfo
    }

    $candidates = @(
        @{ Name = 'python3'; Prefix = @() },
        @{ Name = 'python'; Prefix = @() },
        @{ Name = 'py'; Prefix = @('-3') }
    )
    foreach ($candidate in $candidates) {
        $command = Get-Command $candidate.Name -ErrorAction SilentlyContinue
        if ($null -eq $command) {
            continue
        }
        $probeArgs = @($candidate.Prefix) + @('-c', 'import sys')
        try {
            $probe = Invoke-OrigoNative -FilePath $command.Source -Arguments $probeArgs -AllowFailure
            if ($probe.ExitCode -eq 0) {
                $script:OrigoPythonInfo = [pscustomobject]@{
                    File = $command.Source
                    Prefix = $candidate.Prefix
                }
                return $script:OrigoPythonInfo
            }
        }
        catch {
            continue
        }
    }
    $message = 'Python 3 is required for this gate. Install Python 3 and ensure python3, python, or py -3 is on PATH.'
    throw $message
}

function Invoke-OrigoPython {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$WorkingDirectory = (Get-OrigoRoot),
        [switch]$AllowFailure,
        [switch]$Quiet,
        [switch]$PassThru
    )
    $python = Get-OrigoPythonInfo
    # Force UTF-8 mode so shared Python helpers decode subprocess output with
    # UTF-8 instead of the Windows locale code page.
    $env:PYTHONUTF8 = '1'
    $env:PYTHONIOENCODING = 'utf-8'
    $allArgs = @($python.Prefix) + $Arguments
    $result = Invoke-OrigoNative -FilePath $python.File -Arguments $allArgs -WorkingDirectory $WorkingDirectory -AllowFailure:$AllowFailure
    if (-not $Quiet) {
        if (-not [string]::IsNullOrWhiteSpace($result.Output)) {
            Write-Host $result.Output.TrimEnd()
        }
    }
    if (-not $AllowFailure -and $result.ExitCode -ne 0) {
        throw ("python " + ($Arguments -join ' ') + " failed with exit code " + $result.ExitCode)
    }
    if ($PassThru) {
        return $result
    }
}

function ConvertTo-OrigoCommandLine {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $quoted = foreach ($argument in $Arguments) {
        if ($argument -match '[\s"]') {
            '"' + ($argument -replace '"', '\"') + '"'
        }
        else {
            $argument
        }
    }
    return ($quoted -join ' ')
}

function Invoke-OrigoNative {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = (Get-OrigoRoot),
        [switch]$AllowFailure,
        [int]$TimeoutSeconds = 900
    )
    $stdout = [System.IO.Path]::GetTempFileName()
    $stderr = [System.IO.Path]::GetTempFileName()
    try {
        $argumentLine = ConvertTo-OrigoCommandLine $Arguments
        $inner = '"' + $FilePath + '" ' + $argumentLine + ' > "' + $stdout + '" 2> "' + $stderr + '"'
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $env:ComSpec
        $startInfo.Arguments = '/d /s /c "' + $inner + '"'
        $startInfo.WorkingDirectory = $WorkingDirectory
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            throw ("Failed to start '" + $FilePath + "'.")
        }
        $exited = $process.WaitForExit($TimeoutSeconds * 1000)
        if (-not $exited) {
            & taskkill.exe /PID $process.Id /T /F 2>&1 | Out-Null
            $process.WaitForExit()
            $message = "'" + $FilePath + " " + $argumentLine + "' timed out after " + $TimeoutSeconds + " seconds."
            throw $message
        }
        $out = Get-Content -LiteralPath $stdout -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
        $err = Get-Content -LiteralPath $stderr -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($null -eq $out) { $out = '' }
        if ($null -eq $err) { $err = '' }
        $combined = $out
        if ($err -ne '') {
            $combined = $combined + $err
        }
        $exitCode = $process.ExitCode
        if (-not $AllowFailure -and $exitCode -ne 0) {
            $message = "'" + $FilePath + " " + $argumentLine + "' failed with exit code " + $exitCode + [Environment]::NewLine + $combined
            throw $message
        }
        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = $combined
        }
    }
    finally {
        Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    }
}

function New-OrigoTempDirectory {
    $path = Join-Path ([System.IO.Path]::GetTempPath()) ('origo-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $path | Out-Null
    return $path
}

function Remove-OrigoPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Set-OrigoGodotUserData {
    $appData = Join-OrigoPath @((Get-OrigoRoot), '.godot-home', 'AppData', 'Roaming')
    $localAppData = Join-OrigoPath @((Get-OrigoRoot), '.godot-home', 'AppData', 'Local')
    New-Item -ItemType Directory -Force -Path $appData, $localAppData | Out-Null
    $env:APPDATA = $appData
    $env:LOCALAPPDATA = $localAppData
}

function Get-OrigoGodotBinary {
    $version = Get-OrigoGodotSdkVersion
    $cache = Join-OrigoPath @((Get-OrigoRoot), '.godot_binary', $version)
    if (Test-Path -LiteralPath $cache) {
        $cached = Get-ChildItem -LiteralPath $cache -Recurse -File -Filter 'Godot*_mono_win64.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $cached) {
            return $cached.FullName
        }
    }

    New-Item -ItemType Directory -Force -Path $cache | Out-Null
    $archive = Join-OrigoPath @($cache, ('Godot_v' + $version + '-stable_mono_win64.zip'))
    $url = 'https://github.com/godotengine/godot-builds/releases/download/' + $version
    $url = $url + '-stable/Godot_v' + $version + '-stable_mono_win64.zip'
    Write-Host ("Downloading Godot " + $version + " from " + $url)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $cache -Force
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    $binary = Get-ChildItem -LiteralPath $cache -Recurse -File -Filter 'Godot*_mono_win64.exe' | Select-Object -First 1
    if ($null -eq $binary) {
        throw "Could not find the Godot executable under $cache."
    }
    return $binary.FullName
}
