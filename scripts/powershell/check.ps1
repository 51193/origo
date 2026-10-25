# Layered verification for Windows: plan, quick, affected, or full.

param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('plan', 'quick', 'affected', 'full')]
    [string]$Mode,
    [string]$Project,
    [string]$Filter,
    [string]$Base,
    [string]$Plan,
    [string]$Output,
    [switch]$DirectTests
)

. (Join-Path $PSScriptRoot 'common.ps1')
Resolve-OrigoDotnet | Out-Null

if ($Mode -eq 'full') {
    if ($DirectTests) {
        & (Join-Path $PSScriptRoot 'ci.ps1') -DirectTests
    }
    else {
        & (Join-Path $PSScriptRoot 'ci.ps1')
    }
    & (Join-Path $PSScriptRoot 'lint-commits.ps1')
    exit 0
}

$arguments = @('scripts/python/check.py', $Mode)
if (-not [string]::IsNullOrWhiteSpace($Project)) { $arguments += @('--project', $Project) }
if (-not [string]::IsNullOrWhiteSpace($Filter)) { $arguments += @('--filter', $Filter) }
if (-not [string]::IsNullOrWhiteSpace($Base)) { $arguments += @('--base', $Base) }
if (-not [string]::IsNullOrWhiteSpace($Plan)) { $arguments += @('--plan', $Plan) }
if (-not [string]::IsNullOrWhiteSpace($Output)) { $arguments += @('--output', $Output) }

Invoke-OrigoPython -Arguments $arguments
