# Full Windows development gate, mirroring scripts/bash/ci.sh for the PowerShell suite.

param(
    [switch]$DirectTests
)

. (Join-Path $PSScriptRoot 'common.ps1')
Resolve-OrigoDotnet | Out-Null

$steps = @(
    'lint-scripts.ps1',
    'format.ps1',
    'api-inventory.ps1',
    'test.ps1',
    'benchmark.ps1',
    'godot-test.ps1',
    'package-consumer-smoke.ps1'
)

foreach ($step in $steps) {
    Write-Host ''
    Write-Host ('>>> ' + $step)
    if ($DirectTests -and $step -eq 'test.ps1') {
        & (Join-Path $PSScriptRoot $step) -Direct
    }
    elseif ($DirectTests -and $step -eq 'benchmark.ps1') {
        & (Join-Path $PSScriptRoot $step) -Direct
    }
    else {
        & (Join-Path $PSScriptRoot $step)
    }
}

Write-Host ''
Write-Host 'All Windows CI steps passed.'
