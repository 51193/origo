# Regression: release-commit detection must survive cmd.exe argument quoting.

. (Join-Path $PSScriptRoot 'common.ps1')

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('origo-parent-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
function Invoke-TestGit {
    param([string[]]$Arguments)
    return Invoke-OrigoNative -FilePath 'git' -Arguments $Arguments -WorkingDirectory $temp
}
try {
    Invoke-TestGit @('init', '--quiet') | Out-Null
    Invoke-TestGit @('config', 'user.email', 'test@example.invalid') | Out-Null
    Invoke-TestGit @('config', 'user.name', 'Origo Test') | Out-Null
    Set-Content -LiteralPath (Join-Path $temp 'Directory.Build.props') -Value 'first'
    Invoke-TestGit @('add', 'Directory.Build.props') | Out-Null
    Invoke-TestGit @('commit', '--quiet', '-m', 'initial') | Out-Null
    $first = (Invoke-TestGit @('rev-parse', 'HEAD')).Output.Trim()

    Set-Content -LiteralPath (Join-Path $temp 'Directory.Build.props') -Value 'second'
    Invoke-TestGit @('commit', '--quiet', '-am', 'chore(release): sync version stamps to v0.1.2') | Out-Null
    $head = (Invoke-TestGit @('rev-parse', 'HEAD')).Output.Trim()

    $parent = Get-OrigoParentCommit -Commit $head -WorkingDirectory $temp
    if ($parent -ne $first) {
        throw ('Expected parent ' + $first + ', got ' + $parent)
    }
    if (-not (Test-OrigoMetadataReleaseCommit -Commit $head -WorkingDirectory $temp)) {
        throw 'Expected the metadata-only release commit to be recognized.'
    }

    Set-Content -LiteralPath (Join-Path $temp 'unexpected.txt') -Value 'not metadata'
    Invoke-TestGit @('add', 'unexpected.txt') | Out-Null
    Invoke-TestGit @('commit', '--quiet', '-m', 'chore(release): sync version stamps to v0.1.3') | Out-Null
    $extra = (Invoke-TestGit @('rev-parse', 'HEAD')).Output.Trim()
    if (Test-OrigoMetadataReleaseCommit -Commit $extra -WorkingDirectory $temp) {
        throw 'A release-commit subject with an extra changed file must be rejected.'
    }

    Write-Host 'PowerShell parent/release-commit regression: OK'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
