# Install the .NET SDK pinned by global.json on Windows.
# Mirrors scripts/install-dotnet.sh without invoking Git Bash.

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-OrigoRoot
$globalJson = Get-Content -LiteralPath (Join-Path $root 'global.json') -Raw | ConvertFrom-Json
$required = [string]$globalJson.sdk.version
if ([string]::IsNullOrWhiteSpace($required)) {
    throw 'Could not parse sdk.version from global.json.'
}

$target = $null
if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_INSTALL_DIR)) {
    $target = $env:DOTNET_INSTALL_DIR
    if (-not [System.IO.Path]::IsPathRooted($target)) {
        $target = Join-Path $root $target
    }
}
elseif (Test-Path -LiteralPath (Join-Path $root '.dotnet')) {
    $target = Join-Path $root '.dotnet'
}
elseif (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE) -and (Test-Path -LiteralPath $env:USERPROFILE)) {
    $target = Join-Path $env:USERPROFILE '.dotnet'
}
else {
    $target = Join-Path $root '.dotnet'
}

$target = [System.IO.Path]::GetFullPath($target)
if ($target.TrimEnd('\') -eq $root.TrimEnd('\')) {
    throw 'The SDK target must be an SDK root, not the repository root. Set DOTNET_INSTALL_DIR to an SDK directory.'
}

$cliHome = Join-Path $root '.dotnet-cli-home'
New-Item -ItemType Directory -Force -Path $cliHome | Out-Null
$env:DOTNET_CLI_HOME = $cliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$candidate = Join-Path $target 'dotnet.exe'
if (Test-Path -LiteralPath $candidate -PathType Leaf) {
    $probe = Invoke-OrigoNative -FilePath $candidate -Arguments @('--version') -WorkingDirectory $root -AllowFailure
    if ($probe.ExitCode -eq 0) {
        Write-Host ('The .NET SDK at ' + $target + ' already satisfies ' + (Join-Path $root 'global.json') + '.')
        Write-Host 'Nothing to install.'
        exit 0
    }
}

try {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $probeWrite = Join-Path $target '.origo-write-probe'
    Set-Content -LiteralPath $probeWrite -Value 'probe' -ErrorAction Stop
    Remove-Item -LiteralPath $probeWrite -Force -ErrorAction SilentlyContinue
}
catch {
    $target = Join-Path $root '.dotnet'
    New-Item -ItemType Directory -Force -Path $target | Out-Null
}
$installer = Join-Path ([System.IO.Path]::GetTempPath()) ('origo-dotnet-install-' + [guid]::NewGuid().ToString('N') + '.ps1')
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Host 'Downloading dotnet-install.ps1...'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer

    Write-Host ('Installing .NET SDK ' + $required + ' to ' + $target)
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Version $required -InstallDir $target -NoPath
    if ($LASTEXITCODE -ne 0) {
        throw ('dotnet-install.ps1 failed with exit code ' + $LASTEXITCODE)
    }

    $installed = Join-Path $target 'dotnet.exe'
    $env:DOTNET_ROOT = $target
    $env:PATH = $target + ';' + $env:PATH
    $verify = Invoke-OrigoNative -FilePath $installed -Arguments @('--version') -WorkingDirectory $root -AllowFailure
    if ($verify.ExitCode -ne 0) {
        $message = 'The SDK installed at ' + $target + ' does not satisfy ' + (Join-Path $root 'global.json') + '.' + [Environment]::NewLine + $verify.Output
        throw $message
    }
    Write-Host ''
    Write-Host ('Installed .NET SDK ' + $required + ' to: ' + $target)
    Write-Host ('Verified selected version: ' + $verify.Output.Trim())
    Write-Host 'Run the Windows gates from this checkout, for example:'
    Write-Host '  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows\ci.ps1'
}
finally {
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
}
