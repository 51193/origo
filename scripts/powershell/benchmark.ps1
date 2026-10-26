# Performance benchmark gate for Windows.
# Runs the benchmark categories and compares against docs/benchmarks/baseline.json
# when the baseline was captured on this machine.

param(
    [switch]$UpdateBaseline,
    [switch]$Direct
)

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-OrigoRoot
$baselinePath = Join-Path $root 'docs\benchmarks\baseline.json'
$opsThreshold = 0.50
$allocThreshold = 0.20
$machineId = [Environment]::MachineName
$projects = @(
    'Origo.SourceGeneration.Tests\Origo.SourceGeneration.Tests.csproj',
    'Origo.Core.Tests\Origo.Core.Tests.csproj',
    'Origo.GodotAdapter.Tests\Origo.GodotAdapter.Tests.csproj'
)

$metrics = @{}
$runFailed = $false
if ($Direct) {
    Invoke-OrigoDotnetStreaming -Arguments @('restore', 'Origo.sln')
    Invoke-OrigoDotnetStreaming -Arguments @('build', 'Origo.sln', '--no-restore', '--configuration', 'Release', '-p:UseSharedCompilation=false')
}
foreach ($project in $projects) {
    Write-OrigoHeader ('Benchmark: ' + $project)
    if ($Direct) {
        $result = Invoke-OrigoNative -FilePath (Resolve-OrigoDotnet) -Arguments @(
            'run', '--project', $project, '--configuration', 'Release', '--no-build',
            '--', '-noLogo', '-noColor', '-trait', 'Category=Benchmark'
        ) -AllowFailure
    }
    else {
        $result = Invoke-OrigoDotnet -Arguments @(
            'test', $project,
            '--configuration', 'Release',
            '--filter', 'Category=Benchmark',
            '--logger', 'console;verbosity=detailed',
            '-p:CollectCoverage=false',
            '-p:UseSharedCompilation=false'
        ) -AllowFailure -PassThru
    }
    Write-Host $result.Output.TrimEnd()
    if ($result.ExitCode -ne 0) {
        $runFailed = $true
    }
    foreach ($line in ($result.Output -split "`r?`n")) {
        $match = [regex]::Match($line, '^\s*BENCH\|([^|]+)\|([^|]*)\|([^|]*)\|([0-9.]+)\|(-?[0-9]+)\s*$')
        if (-not $match.Success) { continue }
        $kind = $match.Groups[1].Value
        $label = $match.Groups[2].Value
        $side = $match.Groups[3].Value
        $key = if ($side -ne '') { $kind + '|' + $label + '|' + $side } else { $kind + '|' + $label }
        if ($metrics.ContainsKey($key)) {
            throw ('Duplicate BENCH metric key in one run: ' + $key)
        }
        $metrics[$key] = [pscustomobject]@{
            ops = [double]$match.Groups[4].Value
            alloc = [long]$match.Groups[5].Value
        }
    }
}

if ($runFailed) {
    throw 'Benchmark run failed; no comparison was performed.'
}
if ($metrics.Count -eq 0) {
    throw 'No BENCH metrics were parsed from the benchmark run.'
}

if (-not (Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
    if (-not $UpdateBaseline) {
        throw ('Benchmark baseline not found: ' + $baselinePath)
    }
    $document = [ordered]@{
        schema_version = 1
        machine_id = $machineId
        metrics = $metrics
    }
    $json = $document | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($baselinePath, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ('Benchmark baseline written: ' + $baselinePath)
    exit 0
}

if ($UpdateBaseline) {
    $document = [ordered]@{
        schema_version = 1
        machine_id = $machineId
        metrics = $metrics
    }
    $json = $document | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($baselinePath, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ('Benchmark baseline updated: ' + $baselinePath)
    exit 0
}

$baseline = Get-Content -LiteralPath $baselinePath -Raw -Encoding UTF8 | ConvertFrom-Json
$sameMachine = [string]$baseline.machine_id -eq $machineId
if (-not $sameMachine) {
    Write-Host ('Baseline machine ''' + $baseline.machine_id + ''' != current ''' + $machineId + '''.')
    Write-Host 'Regression gates SKIPPED (metrics are not comparable across machines);'
    Write-Host 'benchmark run still acts as a smoke test.'
}

$failures = New-Object System.Collections.Generic.List[string]
$throughputRegressed = $false
$skipped = 0
$throughputKinds = @('CompareTable', 'Compare', 'Report')
$baselineMetrics = $baseline.metrics
foreach ($property in $baselineMetrics.PSObject.Properties) {
    $key = $property.Name
    if (-not $metrics.ContainsKey($key)) { continue }
    $base = $property.Value
    $current = $metrics[$key]
    if (-not $sameMachine) { continue }
    $allocRegressed = $false
    if ([long]$base.alloc -eq 0) {
        $allocRegressed = [long]$current.alloc -gt 0
    }
    else {
        $allocRegressed = ([double]$current.alloc / [double]$base.alloc) -gt (1.0 + $allocThreshold)
    }
    if ($allocRegressed) {
        $failures.Add('  FAIL allocation: ' + $key + ' ' + $current.alloc + ' B vs baseline ' + $base.alloc + ' B')
    }
    $kind = ($key -split '\|', 2)[0]
    if ($throughputKinds -contains $kind -and [double]$base.ops -gt 0) {
        if (([double]$current.ops / [double]$base.ops) -lt (1.0 - $opsThreshold)) {
            $throughputRegressed = $true
            $failures.Add('  FAIL throughput: ' + $key + ' ' + [math]::Round($current.ops) + ' ops/s vs baseline ' + [math]::Round($base.ops))
        }
    }
}
$baselineKeys = @($baselineMetrics.PSObject.Properties | ForEach-Object { $_.Name })
foreach ($key in $metrics.Keys) {
    if (-not ($baselineKeys -contains $key)) {
        $skipped++
        Write-Host ('  NEW metric (no baseline): ' + $key)
    }
}
if ($skipped -gt 0) {
    Write-Host ('  ' + $skipped + ' metric(s) without baseline entries (new benchmarks).')
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host ('BENCHMARK REGRESSION DETECTED - ' + $failures.Count + ' violation(s):')
    foreach ($failure in $failures) { Write-Host $failure }
    Write-Host ''

    # Match the POSIX gate: a throughput-only comparison failure can be a
    # transient scheduling artifact. Retry once in a fresh process before
    # failing; allocation failures are deterministic and fail immediately.
    $allocationFailed = @($failures | Where-Object { $_ -like '*FAIL allocation:*' }).Count -gt 0
    if ($throughputRegressed -and -not $allocationFailed -and $env:ORIGO_BENCHMARK_RETRY -ne '1') {
        $env:ORIGO_BENCHMARK_RETRY = '1'
        Write-Host 'Throughput-only regression; retrying the benchmark once in a fresh process...'
        & $PSCommandPath @PSBoundParameters
        exit $LASTEXITCODE
    }

    Write-Host 'If the change is a real improvement or the environment changed, rerun with -UpdateBaseline.'
    throw 'Benchmark regression detected.'
}

Write-Host 'All benchmark metrics within thresholds.'
