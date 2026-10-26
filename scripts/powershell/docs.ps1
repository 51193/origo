# Generate or validate the committed documentation artifacts on Windows.
param(
    [ValidateSet('generate', 'check', 'check-staged')]
    [string]$Mode = 'check'
)

. (Join-Path $PSScriptRoot 'common.ps1')

Invoke-OrigoPython -Arguments @('scripts/python/docs_generate.py', $Mode)
