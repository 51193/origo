# Regression: parent-commit lookup must survive cmd.exe argument quoting.

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
    Set-Content -LiteralPath (Join-Path $temp 'probe.txt') -Value 'one'
    Invoke-TestGit @('add', 'probe.txt') | Out-Null
    Invoke-TestGit @('commit', '--quiet', '-m', 'first') | Out-Null
    $first = (Invoke-TestGit @('rev-parse', 'HEAD')).Output.Trim()
    Set-Content -LiteralPath (Join-Path $temp 'probe.txt') -Value 'two'
    Invoke-TestGit @('commit', '--quiet', '-am', 'second') | Out-Null
    $head = (Invoke-TestGit @('rev-parse', 'HEAD')).Output.Trim()
    $parent = Get-OrigoParentCommit -Commit $head -WorkingDirectory $temp
    if ($parent -ne $first) {
        throw ('Expected parent ' + $first + ', got ' + $parent)
    }
    if ($parent -eq $head) {
        throw 'Parent resolution returned HEAD.'
    }
    Write-Host 'PowerShell parent-commit regression: OK'
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
