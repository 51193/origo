# Godot headless integration test gate for Windows.

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-OrigoRoot
$project = Join-Path $root 'Origo.GodotAdapter.Integration.Tests'

function Invoke-OrigoDemoAcceptanceSmoke {
    param([Parameter(Mandatory = $true)][string]$GodotBinary)
    $demoRoot = Join-Path $root 'samples/Origo.EvolutionDemo'
    $demoProject = Join-Path $demoRoot 'Origo.EvolutionDemo.csproj'
    Write-Host 'Building Evolution Demo for the headless acceptance smoke...'
    Invoke-OrigoDotnet -Arguments @('build', $demoProject, '--configuration', 'Debug', '--nologo', '--verbosity', 'quiet')
    $smokeHome = Join-Path ([System.IO.Path]::GetTempPath()) ('origo-demo-smoke-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path (Join-Path $smokeHome 'AppData/Roaming'), (Join-Path $smokeHome 'AppData/Local') | Out-Null
    $environmentNames = @(
        'APPDATA',
        'LOCALAPPDATA',
        'HOME',
        'ORIGO_DEMO_SMOKE',
        'ORIGO_DEMO_SMOKE_SECONDS',
        'ORIGO_DEMO_SMOKE_FAST_FORWARD',
        'ORIGO_DEMO_SMOKE_CHECKPOINT_INTERVAL',
        'ORIGO_DEMO_SMOKE_SAVE_SLOT',
        'ORIGO_DEMO_CAMPAIGN_AUTOPLAY'
    )
    $previous = @{}
    foreach ($name in $environmentNames) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    try {
        $env:APPDATA = Join-Path $smokeHome 'AppData/Roaming'
        $env:LOCALAPPDATA = Join-Path $smokeHome 'AppData/Local'
        $env:HOME = $smokeHome
        $env:ORIGO_DEMO_SMOKE = '1'
        $env:ORIGO_DEMO_SMOKE_SECONDS = '2'
        $env:ORIGO_DEMO_SMOKE_FAST_FORWARD = '65'
        $env:ORIGO_DEMO_SMOKE_CHECKPOINT_INTERVAL = '30'
        $env:ORIGO_DEMO_SMOKE_SAVE_SLOT = 'ci_smoke'
        $env:ORIGO_DEMO_CAMPAIGN_AUTOPLAY = '1'
        $smoke = Invoke-OrigoNative -FilePath $GodotBinary -Arguments @(
            '--headless', '--path', $demoRoot, '--quit-after', '60000'
        ) -WorkingDirectory $root -AllowFailure -TimeoutSeconds 300
        if ($smoke.ExitCode -ne 0) {
            Write-Host $smoke.Output
            throw ('Evolution Demo acceptance smoke failed with exit code ' + $smoke.ExitCode + '.')
        }
        if ($smoke.Output -notmatch 'DEMO_BENCHMARK_OK') {
            Write-Host $smoke.Output
            throw 'Evolution Demo acceptance smoke did not report DEMO_BENCHMARK_OK.'
        }
        if ($smoke.Output -notmatch 'DEMO_CAMERA_OK') {
            Write-Host $smoke.Output
            throw 'Evolution Demo acceptance smoke did not report DEMO_CAMERA_OK.'
        }
        if ($smoke.Output -notmatch 'DEMO_ITERATE_CHECKPOINTS count=2([^0-9]|$)') {
            Write-Host $smoke.Output
            throw 'Evolution Demo acceptance smoke did not produce the two expected checkpoints.'
        }
    }
    finally {
        foreach ($name in $environmentNames) {
            $value = $previous[$name]
            if ($null -eq $value) {
                Remove-Item -LiteralPath ('Env:' + $name) -ErrorAction SilentlyContinue
            }
            else {
                Set-Item -LiteralPath ('Env:' + $name) -Value $value
            }
        }
        Remove-OrigoPath $smokeHome
    }
}
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

Invoke-OrigoDemoAcceptanceSmoke -GodotBinary $godot
Write-Host 'Evolution Demo acceptance smoke passed.'
