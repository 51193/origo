# Format gate for Windows: dotnet format --verify-no-changes --severity info.

. (Join-Path $PSScriptRoot 'common.ps1')

Write-OrigoHeader 'Format check (dotnet format --verify-no-changes --severity info)'
Invoke-OrigoDotnet -Arguments @('restore', 'Origo.sln', '--verbosity', 'quiet')
Invoke-OrigoDotnet -Arguments @('format', 'Origo.sln', '--verify-no-changes', '--severity', 'info')
Invoke-OrigoDotnet -Arguments @('format', 'analyzers', 'Origo.sln', '--verify-no-changes', '--severity', 'info', '--diagnostics', 'IDE0051', 'IDE0052')
Write-Host 'Format: OK'
