# Godot headless integration test gate for Windows.

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-OrigoRoot
$project = Join-Path $root 'Origo.GodotAdapter.Integration.Tests'
$adapterSdk = Get-OrigoGodotSdkVersion
$integrationSdk = Get-OrigoGodotSdkVersion -ProjectPath (Join-Path $project 'Origo.GodotAdapter.Integration.Tests.csproj')
if ($adapterSdk -ne $integrationSdk) {
    throw ('Godot.NET.Sdk pin mismatch: adapter=' + $adapterSdk + ' integration=' + $integrationSdk + '. Update both projects in the same change.')
}
$godot = Get-OrigoGodotBinary
Set-OrigoGodotUserData

Write-Host ('Using Godot binary: ' + $godot)
Write-Host 'Cleaning Godot build caches...'
Remove-OrigoPath (Join-Path $root 'Origo.GodotAdapter\.godot')
Remove-OrigoPath (Join-Path $project '.godot')

Write-Host 'Building integration test project...'
Invoke-OrigoDotnet -Arguments @('build', (Join-Path $project 'Origo.GodotAdapter.Integration.Tests.csproj'), '--verbosity', 'quiet')

$result = Invoke-OrigoNative -FilePath $godot -Arguments @('--headless', '--path', $project) -WorkingDirectory $root -AllowFailure
$output = $result.Output
$match = [regex]::Match($output, 'INTEGRATION_TEST_RESULTS:\s+([0-9]+)\s+total')
$total = 0
if ($match.Success) {
    $total = [int]$match.Groups[1].Value
}

if ($total -le 0) {
    Write-Host $output
    throw 'Integration tests failed: no positive INTEGRATION_TEST_RESULTS count was produced.'
}
if ($output -match 'ObjectDB instances were leaked') {
    Write-Host $output
    throw 'Integration tests failed: Godot reported leaked ObjectDB instances.'
}
if ($result.ExitCode -ne 0) {
    Write-Host $output
    throw ('Integration tests failed with exit code ' + $result.ExitCode + ' (total: ' + $total + ').')
}

Write-Host ('All ' + $total + ' integration tests passed.')
