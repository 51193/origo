# Shell API baseline gate for Windows.
# Builds the shell projects, verifies the tracked Roslyn inventory, and
# compares against the newest formal release baseline when one is available.

param(
    [ValidateSet('verify', 'generate')]
    [string]$Mode = 'verify'
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-OrigoRoot
$baseline = Join-Path $root 'tools\ApiInventoryTool\shell-api-baseline.json'

function Get-OrigoTargetDir {
    param([string]$Project)
    $result = Invoke-OrigoDotnet -Arguments @('msbuild', $Project, '-getProperty:TargetDir', '-p:Configuration=Release', '-nologo') -PassThru -Quiet
    $dir = $result.Output.Trim()
    if (-not $dir.EndsWith('\')) {
        $dir = $dir + '\'
    }
    return $dir
}

function Test-OrigoVersionNewer {
    param([string]$Candidate, [string]$Reference)
    return ((ConvertTo-OrigoVersion $Candidate) -gt (ConvertTo-OrigoVersion $Reference))
}

function Test-OrigoMetadataReleaseCommit {
    param([string]$Commit)
    $parent = Get-OrigoParentCommit -Commit $Commit
    if ([string]::IsNullOrWhiteSpace($parent)) { return $false }
    $ancestor = Invoke-OrigoNative -FilePath 'git' -Arguments @('merge-base', '--is-ancestor', $parent, 'HEAD') -AllowFailure
    if ($ancestor.ExitCode -ne 0) { return $false }
    $subject = Invoke-OrigoNative -FilePath 'git' -Arguments @('log', '-1', '--format=%s', $Commit) -AllowFailure
    if ($subject.ExitCode -ne 0 -or $subject.Output.Trim() -notmatch '^chore\(release\): sync version stamps to v[0-9]+\.[0-9]+\.[0-9]+$') {
        return $false
    }
    $changed = Invoke-OrigoNative -FilePath 'git' -Arguments @('diff', '--name-only', $parent, $Commit) -AllowFailure
    if ($changed.ExitCode -ne 0) { return $false }
    foreach ($path in ($changed.Output -split "`r?`n")) {
        if ($path.Trim() -ne '' -and $path.Trim() -ne 'Directory.Build.props') { return $false }
    }
    return $true
}

function Find-OrigoPreviousApiBaseline {
    $currentCommit = (Invoke-OrigoNative -FilePath 'git' -Arguments @('rev-parse', 'HEAD')).Output.Trim()
    $currentTag = [string]$env:ORIGO_CURRENT_RELEASE_TAG
    $currentVersion = ''
    if ($currentTag -match '^v[0-9]+\.[0-9]+\.[0-9]+$') {
        $currentVersion = $currentTag.Substring(1)
    }
    else {
        $currentVersion = Get-OrigoVersionBase (Get-OrigoBuildVersion)
    }
    if ($currentVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
        throw "Source version '$currentVersion' has no usable x.y.z form."
    }

    $tags = (Invoke-OrigoNative -FilePath 'git' -Arguments @('tag', '--list', 'v*', '--sort=-version:refname')).Output -split "`r?`n"
    foreach ($tag in $tags) {
        $tag = $tag.Trim()
        if ($tag -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+$') { continue }
        if ($tag -eq $currentTag) { continue }
        $tagVersion = $tag.Substring(1)
        if (Test-OrigoVersionNewer $tagVersion $currentVersion) { continue }

        $tagCommitResult = Invoke-OrigoNative -FilePath 'git' -Arguments @('rev-list', '-n', '1', $tag) -AllowFailure
        if ($tagCommitResult.ExitCode -ne 0) { continue }
        $tagCommit = $tagCommitResult.Output.Trim()
        if ($tagCommit -eq $currentCommit) { continue }

        $ancestor = Invoke-OrigoNative -FilePath 'git' -Arguments @('merge-base', '--is-ancestor', $tagCommit, 'HEAD') -AllowFailure
        if ($ancestor.ExitCode -ne 0 -and -not (Test-OrigoMetadataReleaseCommit $tagCommit)) { continue }

        $exists = Invoke-OrigoNative -FilePath 'git' -Arguments @('cat-file', '-e', ($tag + ':tools/ApiInventoryTool/shell-api-baseline.json')) -AllowFailure
        if ($exists.ExitCode -ne 0) { continue }

        $temp = Join-Path ([System.IO.Path]::GetTempPath()) ('origo-api-baseline-' + [guid]::NewGuid().ToString('N') + '.json')
        $show = Invoke-OrigoNative -FilePath 'git' -Arguments @('show', ($tag + ':tools/ApiInventoryTool/shell-api-baseline.json')) -AllowFailure
        if ($show.ExitCode -ne 0) { continue }
        [System.IO.File]::WriteAllText($temp, $show.Output, (New-Object System.Text.UTF8Encoding($false)))
        return $temp
    }
    return $null
}

Write-OrigoHeader ('Shell API inventory: ' + $Mode)
Invoke-OrigoDotnet -Arguments @('build', 'Origo.Core.Contracts\Origo.Core.Contracts.csproj', '--configuration', 'Release')
Invoke-OrigoDotnet -Arguments @('build', 'Origo.Core\Origo.Core.csproj', '--configuration', 'Release')
Invoke-OrigoDotnet -Arguments @('build', 'Origo.GodotAdapter\Origo.GodotAdapter.csproj', '--configuration', 'Release')
Invoke-OrigoDotnet -Arguments @('build', 'Origo.ConsoleBridge\Origo.ConsoleBridge.csproj', '--configuration', 'Release')

$contractsDir = Get-OrigoTargetDir 'Origo.Core.Contracts\Origo.Core.Contracts.csproj'
$coreDir = Get-OrigoTargetDir 'Origo.Core\Origo.Core.csproj'
$adapterDir = Get-OrigoTargetDir 'Origo.GodotAdapter\Origo.GodotAdapter.csproj'
$bridgeDir = Get-OrigoTargetDir 'Origo.ConsoleBridge\Origo.ConsoleBridge.csproj'

$assemblyArgs = @(
    '--assembly', ('Origo.Core.Contracts=' + $contractsDir + 'Origo.Core.Contracts.dll'),
    '--assembly', ('Origo.Core=' + $coreDir + 'Origo.Core.dll'),
    '--assembly', ('Origo.GodotAdapter=' + $adapterDir + 'Origo.GodotAdapter.dll'),
    '--assembly', ('Origo.ConsoleBridge=' + $bridgeDir + 'Origo.ConsoleBridge.dll')
)
$referenceArgs = @(
    '--reference-dir', $contractsDir,
    '--reference-dir', $coreDir,
    '--reference-dir', $adapterDir,
    '--reference-dir', $bridgeDir
)

$toolArgs = @('run', '--project', 'tools\ApiInventoryTool', '--')
if ($Mode -eq 'generate') {
    $toolArgs += @('generate') + $assemblyArgs + $referenceArgs + @('--output', $baseline)
}
else {
    $toolArgs += @('verify') + $assemblyArgs + $referenceArgs + @('--baseline', $baseline)
}
Invoke-OrigoDotnet -Arguments $toolArgs

$previous = [string]$env:ORIGO_PREVIOUS_API_BASELINE
$autoPrevious = $null
if ([string]::IsNullOrWhiteSpace($previous)) {
    $autoPrevious = Find-OrigoPreviousApiBaseline
    $previous = [string]$autoPrevious
}

try {
    if (-not [string]::IsNullOrWhiteSpace($previous)) {
        if (-not (Test-Path -LiteralPath $previous -PathType Leaf)) {
            throw ('Previous shell API baseline does not exist: ' + $previous)
        }
        Write-Host ''
        Write-Host ('Previous-package validation: using ' + $(if ($env:ORIGO_PREVIOUS_API_BASELINE) { 'explicit' } else { 'auto-detected' }) + ' shell API baseline.')
        $current = Join-Path ([System.IO.Path]::GetTempPath()) ('origo-api-current-' + [guid]::NewGuid().ToString('N') + '.json')
        try {
            $generateArgs = @('run', '--project', 'tools\ApiInventoryTool', '--', 'generate') + $assemblyArgs + $referenceArgs + @('--output', $current)
            Invoke-OrigoDotnet -Arguments $generateArgs
            $compareArgs = @('run', '--project', 'tools\ApiInventoryTool', '--', 'compare', '--previous', $previous, '--current', $current)
            Invoke-OrigoDotnet -Arguments $compareArgs
        }
        finally {
            Remove-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
        }
    }
    else {
        Write-Host ''
        Write-Host 'Previous-package validation: no previous formal release shell API baseline was found.'
        Write-Host 'Set ORIGO_PREVIOUS_API_BASELINE to override auto-detection.'
    }
    Write-Host 'Shell API inventory: OK'
}
finally {
    if ($null -ne $autoPrevious) {
        Remove-Item -LiteralPath $autoPrevious -Force -ErrorAction SilentlyContinue
    }
}
