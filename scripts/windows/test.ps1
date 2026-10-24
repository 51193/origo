# Build + test + Coverlet line coverage gate for Windows.
# Mirrors scripts/test.sh without invoking Git Bash.

param(
    [string]$Configuration = 'Release',
    [switch]$Direct
)

. (Join-Path $PSScriptRoot 'common.ps1')

Write-OrigoHeader 'Origo Windows test gate (build + tests + line coverage >= 90%)'
if ($Direct) {
    Write-Host 'Direct xUnit runner mode: runs every test project without VSTest/Coverlet.'
    Invoke-OrigoDotnetStreaming -Arguments @('restore', 'Origo.sln')
    Invoke-OrigoDotnetStreaming -Arguments @('build', 'Origo.sln', '--no-restore', '--configuration', $Configuration, '-p:UseSharedCompilation=false')
    $directProjects = @(
        'Origo.Core.Tests\Origo.Core.Tests.csproj',
        'Origo.ConsoleBridge.Tests\Origo.ConsoleBridge.Tests.csproj',
        'Origo.GodotAdapter.Tests\Origo.GodotAdapter.Tests.csproj',
        'Origo.SourceGeneration.Tests\Origo.SourceGeneration.Tests.csproj',
        'tools\ApiInventoryTool.Tests\ApiInventoryTool.Tests.csproj'
    )
    foreach ($project in $directProjects) {
        Write-Host ('>>> ' + $project)
        Invoke-OrigoDotnetStreaming -Arguments @(
            'run', '--project', $project, '--configuration', $Configuration, '--no-build',
            '--', '-noLogo', '-noColor', '-trait-', 'Category=Benchmark'
        )
    }
    Write-Host 'Tests (direct runner): OK'
    exit 0
}

Write-Host 'Coverlet enforces the per-project line coverage threshold from the test csproj files.'

Invoke-OrigoDotnetStreaming -Arguments @('restore', 'Origo.sln')
Invoke-OrigoDotnetStreaming -Arguments @('build', 'Origo.sln', '--no-restore', '--configuration', $Configuration, '-p:UseSharedCompilation=false')

Write-Host ''
Write-Host 'Running tests (benchmarks are excluded; run benchmark.ps1 separately).'
$testProjects = @(
    'Origo.Core.Tests\Origo.Core.Tests.csproj',
    'Origo.ConsoleBridge.Tests\Origo.ConsoleBridge.Tests.csproj',
    'Origo.GodotAdapter.Tests\Origo.GodotAdapter.Tests.csproj',
    'Origo.SourceGeneration.Tests\Origo.SourceGeneration.Tests.csproj',
    'tools\ApiInventoryTool.Tests\ApiInventoryTool.Tests.csproj'
)
foreach ($project in $testProjects) {
    Write-Host ('>>> ' + $project)
    Invoke-OrigoDotnetStreaming -Arguments @(
        'test', $project,
        '--no-build',
        '--configuration', $Configuration,
        '--filter', 'Category!=Benchmark',
        '--logger', 'console;verbosity=minimal',
        '-m:1'
    )
}
Write-Host 'Tests: OK'
