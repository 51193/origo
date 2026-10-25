# Shell-only package consumer smoke test for Windows.
# Packs the five shell/kernel packages, validates the package graph, restores
# the tracked consumer fixtures through PackageReference only, builds them,
# runs the public ConsoleBridge and Godot entry paths, and checks kernel
# compile-asset isolation.

. (Join-Path $PSScriptRoot 'common.ps1')

Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Get-OrigoRoot
$version = Get-OrigoBuildVersion
$versionArg = '-p:OrigoShellPackageVersion=' + $version
$sdkVersion = Get-OrigoGodotSdkVersion
$work = New-OrigoTempDirectory
$feed = Join-Path $work 'feed'
$consumer = Join-Path $work 'consumer'
$probe = Join-Path $work 'kernel-probe'
$consoleConsumer = Join-Path $work 'console-consumer'
$packages = Join-Path $work 'packages'

function Get-OrigoNuspecInfo {
    param([Parameter(Mandatory = $true)][string]$Path)
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') } | Select-Object -First 1
        if ($null -eq $entry) {
            throw ('Package has no nuspec: ' + $Path)
        }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try {
            $text = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
        $xml = [xml]$text
        $metadata = $xml.package.metadata
        $dependencies = @()
        if ($null -ne $metadata.dependencies) {
            foreach ($group in $metadata.dependencies.ChildNodes) {
                if ($group.LocalName -ne 'group') { continue }
                foreach ($dependency in $group.ChildNodes) {
                    if ($dependency.LocalName -ne 'dependency') { continue }
                    $dependencyVersion = [string]$dependency.GetAttribute('version')
                    if ($dependencyVersion.StartsWith('[') -and $dependencyVersion.EndsWith(']')) {
                        $dependencyVersion = $dependencyVersion.Substring(1, $dependencyVersion.Length - 2)
                    }
                    $dependencies += [pscustomobject]@{
                        Id = $dependency.GetAttribute('id')
                        Version = $dependencyVersion
                        Exclude = $dependency.GetAttribute('exclude')
                    }
                }
            }
        }
        return [pscustomobject]@{
            Id = [string]$metadata.id
            Version = [string]$metadata.version
            Dependencies = $dependencies
            Archive = $archive
        }
    }
    catch {
        $archive.Dispose()
        throw
    }
}

function Test-OrigoPackageSet {
    param([string]$Directory, [string]$ExpectedVersion, [string]$GodotVersion)
    $expected = @('Origo.Core.Contracts', 'Origo.Core.Kernel', 'Origo.Core', 'Origo.GodotAdapter', 'Origo.ConsoleBridge')
    $packagesById = @{}
    $archives = @()
    $failures = New-Object System.Collections.Generic.List[string]
    try {
        $files = Get-ChildItem -LiteralPath $Directory -File -Filter '*.nupkg' | Where-Object { $_.Name -notlike '*.symbols.nupkg' }
        foreach ($file in $files) {
            $info = Get-OrigoNuspecInfo $file.FullName
            $archives += $info.Archive
            if ($packagesById.ContainsKey($info.Id)) {
                $failures.Add('duplicate package id: ' + $info.Id)
            }
            $packagesById[$info.Id] = $info
        }
        foreach ($id in $expected) {
            if (-not $packagesById.ContainsKey($id)) {
                $failures.Add('missing required package: ' + $id)
            }
        }
        foreach ($id in $packagesById.Keys) {
            if ($expected -notcontains $id) {
                $failures.Add('unexpected package in release artifact set: ' + $id)
            }
        }
        foreach ($id in $packagesById.Keys) {
            $info = $packagesById[$id]
            if ($info.Version -ne $ExpectedVersion) {
                $failures.Add($id + ': package version ' + $info.Version + ' != release version ' + $ExpectedVersion)
            }
        }
        if ($packagesById.ContainsKey('Origo.GodotAdapter.Kernel')) {
            $failures.Add('Origo.GodotAdapter.Kernel must not exist; Adapter is one shell package.')
        }

        $pairing = @{
            'Origo.ConsoleBridge' = @{ 'Origo.Core.Contracts' = $ExpectedVersion }
            'Origo.Core' = @{ 'Origo.Core.Contracts' = $ExpectedVersion; 'Origo.Core.Kernel' = $ExpectedVersion }
            'Origo.Core.Kernel' = @{ 'Origo.Core.Contracts' = $ExpectedVersion }
            'Origo.GodotAdapter' = @{ 'Origo.Core' = $ExpectedVersion; 'GodotSharp' = $GodotVersion }
        }
        foreach ($id in $pairing.Keys) {
            if (-not $packagesById.ContainsKey($id)) { continue }
            $deps = @{}
            foreach ($dependency in $packagesById[$id].Dependencies) {
                $deps[$dependency.Id] = $dependency.Version
            }
            foreach ($depId in $pairing[$id].Keys) {
                if (-not $deps.ContainsKey($depId)) {
                    $failures.Add($id + ': missing dependency ' + $depId + ' ' + $pairing[$id][$depId])
                }
                elseif ($deps[$depId] -ne $pairing[$id][$depId]) {
                    $failures.Add($id + ': dependency ' + $depId + ' version ' + $deps[$depId] + ' != ' + $pairing[$id][$depId])
                }
            }
        }

        $expectedOrigoDeps = @{
            'Origo.Core.Contracts' = @()
            'Origo.Core.Kernel' = @('Origo.Core.Contracts')
            'Origo.Core' = @('Origo.Core.Contracts', 'Origo.Core.Kernel')
            'Origo.GodotAdapter' = @('Origo.Core')
            'Origo.ConsoleBridge' = @('Origo.Core.Contracts')
        }
        foreach ($id in $expectedOrigoDeps.Keys) {
            if (-not $packagesById.ContainsKey($id)) { continue }
            $actual = @($packagesById[$id].Dependencies | Where-Object { $_.Id -like 'Origo.*' } | ForEach-Object { $_.Id })
            $actualSorted = ($actual | Sort-Object) -join ','
            $expectedSorted = ($expectedOrigoDeps[$id] | Sort-Object) -join ','
            if ($actualSorted -ne $expectedSorted) {
                $failures.Add($id + ': Origo dependency set [' + $actualSorted + '] != expected [' + $expectedSorted + ']')
            }
        }

        if ($packagesById.ContainsKey('Origo.Core.Contracts')) {
            $info = $packagesById['Origo.Core.Contracts']
            $analyzer = $info.Archive.Entries | Where-Object { $_.FullName -eq 'analyzers/dotnet/cs/Origo.SourceGeneration.dll' }
            if ($null -eq $analyzer) {
                $failures.Add('Origo.Core.Contracts is missing analyzers/dotnet/cs/Origo.SourceGeneration.dll')
            }
        }
        foreach ($shell in @('Origo.Core', 'Origo.GodotAdapter', 'Origo.ConsoleBridge')) {
            if (-not $packagesById.ContainsKey($shell)) { continue }
            $leaked = $packagesById[$shell].Archive.Entries | Where-Object { $_.FullName.EndsWith('/Origo.Core.Kernel.dll') }
            if ($null -ne $leaked) {
                $failures.Add($shell + ': kernel implementation assembly embedded')
            }
        }
    }
    finally {
        foreach ($archive in $archives) {
            $archive.Dispose()
        }
    }
    if ($failures.Count -gt 0) {
        foreach ($failure in $failures) { Write-Host ('  - ' + $failure) }
        throw 'Release package validation failed.'
    }
    Write-Host ('Release package set OK: ' + $packagesById.Count + ' packages at exact version ' + $ExpectedVersion + '.')
}

function Get-OrigoAssets {
    param([string]$Directory)
    return Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter 'project.assets.json' | Select-Object -First 1
}

function Test-OrigoAssetsCache {
    param([string]$AssetsPath, [string]$ExpectedCache)
    $assets = Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $expected = [System.IO.Path]::GetFullPath($ExpectedCache).TrimEnd('\')
    foreach ($property in $assets.packageFolders.PSObject.Properties) {
        $actual = [System.IO.Path]::GetFullPath($property.Name).TrimEnd('\')
        if ([string]::Equals($actual, $expected, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Test-OrigoAssetsPackage {
    param([string]$AssetsPath, [string]$PackageId)
    $assets = Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in $assets.libraries.PSObject.Properties) {
        if ($property.Name.StartsWith($PackageId + '/')) { return $true }
    }
    return $false
}

function Invoke-OrigoExpectedBuildFailure {
    param(
        [string]$Project,
        [string]$WorkingDirectory,
        [string]$Label
    )
    $result = Invoke-OrigoDotnet -Arguments @(
        'build', $Project, '--configuration', 'Release', '-warnaserror',
        $versionArg
    ) -WorkingDirectory $WorkingDirectory -AllowFailure -PassThru -Quiet
    if ($result.ExitCode -eq 0) {
        throw ($Label + ' unexpectedly succeeded; kernel compile assets leaked.')
    }
    if ($result.Output -notmatch 'CS0246') {
        Write-Host $result.Output
        throw ($Label + ' failed without the expected CS0246 diagnostic.')
    }
}

try {
    New-Item -ItemType Directory -Force -Path $feed, $consumer, $probe, $consoleConsumer, $packages | Out-Null

    Write-OrigoHeader 'Shell-only package consumer smoke'
    Write-Host (' package version: ' + $version)
    Write-Host (' Godot.NET.Sdk:   ' + $sdkVersion)

    $packProjects = @(
        'Origo.Core.Contracts\Origo.Core.Contracts.csproj',
        'Origo.Core.Kernel\Origo.Core.Kernel.csproj',
        'Origo.Core\Origo.Core.csproj',
        'Origo.GodotAdapter\Origo.GodotAdapter.csproj',
        'Origo.ConsoleBridge\Origo.ConsoleBridge.csproj'
    )
    foreach ($project in $packProjects) {
        Invoke-OrigoDotnet -Arguments @('pack', $project, '--configuration', 'Release', '--output', $feed) -Quiet
    }
    Test-OrigoPackageSet $feed $version $sdkVersion

    Copy-Item -Path (Join-Path $root 'tools\ShellPackageConsumer\*') -Destination $consumer -Recurse -Force
    Copy-Item -Path (Join-Path $root 'tools\ShellPackageConsumer\*') -Destination $probe -Recurse -Force
    Copy-Item -Path (Join-Path $root 'tools\ConsoleBridgePackageConsumer\*') -Destination $consoleConsumer -Recurse -Force

    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-feed" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
    [System.IO.File]::WriteAllText((Join-Path $consumer 'nuget.config'), $nugetConfig, (New-Object System.Text.UTF8Encoding($false)))
    Copy-Item -Path (Join-Path $consumer 'nuget.config') -Destination (Join-Path $probe 'nuget.config') -Force
    Copy-Item -Path (Join-Path $consumer 'nuget.config') -Destination (Join-Path $consoleConsumer 'nuget.config') -Force

    foreach ($fixture in @($consumer, $consoleConsumer)) {
        $csproj = Get-ChildItem -LiteralPath $fixture -File -Filter '*.csproj' | Select-Object -First 1
        if (Select-String -LiteralPath $csproj.FullName -Pattern '<ProjectReference' -Quiet) {
            throw ('Fixture must not contain a ProjectReference: ' + $csproj.FullName)
        }
    }

    $env:NUGET_PACKAGES = $packages

    Invoke-OrigoDotnet -Arguments @('restore', 'OrigoShellPackageConsumer.csproj', $versionArg) -WorkingDirectory $consumer
    $assets = Get-OrigoAssets $consumer
    if ($null -eq $assets) { throw 'Package restore did not produce project.assets.json.' }
    if (-not (Test-OrigoAssetsCache $assets.FullName $packages)) {
        throw 'Package restore did not use the isolated NuGet package cache.'
    }
    foreach ($packageId in @('Origo.Core', 'Origo.GodotAdapter', 'Origo.Core.Contracts', 'Origo.Core.Kernel')) {
        if (-not (Test-OrigoAssetsPackage $assets.FullName $packageId)) {
            throw ('Package restore did not resolve ' + $packageId + '.')
        }
    }
    Invoke-OrigoDotnet -Arguments @('build', 'OrigoShellPackageConsumer.csproj', '--no-restore', '-warnaserror', $versionArg) -WorkingDirectory $consumer
    $kernelDll = Get-ChildItem -LiteralPath (Join-Path $consumer '.godot') -Recurse -File -Filter 'Origo.Core.Kernel.dll' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $kernelDll) { throw 'Kernel runtime assembly was not provided by package restore.' }

    Invoke-OrigoDotnet -Arguments @('restore', 'OrigoConsoleBridgePackageConsumer.csproj', $versionArg) -WorkingDirectory $consoleConsumer
    $consoleAssets = Get-OrigoAssets $consoleConsumer
    if ($null -eq $consoleAssets) { throw 'ConsoleBridge consumer restore did not produce project.assets.json.' }
    if (-not (Test-OrigoAssetsCache $consoleAssets.FullName $packages)) {
        throw 'ConsoleBridge consumer restore did not use the isolated NuGet package cache.'
    }
    foreach ($packageId in @('Origo.ConsoleBridge', 'Origo.Core.Contracts')) {
        if (-not (Test-OrigoAssetsPackage $consoleAssets.FullName $packageId)) {
            throw ('ConsoleBridge consumer restore did not resolve ' + $packageId + '.')
        }
    }
    foreach ($packageId in @('Origo.Core', 'Origo.Core.Kernel')) {
        if (Test-OrigoAssetsPackage $consoleAssets.FullName $packageId) {
            throw ('ConsoleBridge consumer unexpectedly resolved ' + $packageId + '.')
        }
    }
    Invoke-OrigoDotnet -Arguments @('build', 'OrigoConsoleBridgePackageConsumer.csproj', '--no-restore', '--configuration', 'Release', '-warnaserror', $versionArg) -WorkingDirectory $consoleConsumer
    $leakedRuntime = Get-ChildItem -LiteralPath $consoleConsumer -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq 'Origo.Core.dll' -or $_.Name -eq 'Origo.Core.Kernel.dll' } |
        Select-Object -First 1
    if ($null -ne $leakedRuntime) {
        throw 'ConsoleBridge consumer build unexpectedly received Core/Kernel runtime assemblies.'
    }
    $consoleRun = Invoke-OrigoDotnet -Arguments @('run', '--project', 'OrigoConsoleBridgePackageConsumer.csproj', '--configuration', 'Release', '--no-build', '--no-restore') -WorkingDirectory $consoleConsumer -AllowFailure -PassThru
    if ($consoleRun.ExitCode -ne 0) {
        Write-Host $consoleRun.Output
        throw ('ConsoleBridge consumer exited with ' + $consoleRun.ExitCode + '.')
    }
    if ($consoleRun.Output -notmatch 'CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK') {
        Write-Host $consoleRun.Output
        throw 'ConsoleBridge consumer did not print CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK.'
    }
    Write-Host 'CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK'

    Copy-Item -Path (Join-Path $consoleConsumer 'KernelLeakProbe.cs.template') -Destination (Join-Path $consoleConsumer 'KernelLeakProbe.cs') -Force
    Invoke-OrigoExpectedBuildFailure 'OrigoConsoleBridgePackageConsumer.csproj' $consoleConsumer 'ConsoleBridge kernel leak probe'

    Copy-Item -Path (Join-Path $probe 'KernelLeakProbe.cs.template') -Destination (Join-Path $probe 'KernelLeakProbe.cs') -Force
    Invoke-OrigoExpectedBuildFailure 'OrigoShellPackageConsumer.csproj' $probe 'Shell kernel leak probe'

    $godot = Get-OrigoGodotBinary
    Set-OrigoGodotUserData
    Write-Host ('Using Godot binary: ' + $godot)
    Invoke-OrigoNative -FilePath $godot -Arguments @('--headless', '--path', $consumer, '--import') -AllowFailure | Out-Null
    $startup = Invoke-OrigoNative -FilePath $godot -Arguments @('--headless', '--path', $consumer, '--quit-after', '10') -AllowFailure
    if ($startup.ExitCode -ne 0) {
        Write-Host $startup.Output
        throw ('Consumer startup exited with ' + $startup.ExitCode + '.')
    }
    if ($startup.Output -notmatch 'SHELL_CONSUMER_STARTUP_OK') {
        Write-Host $startup.Output
        throw 'Consumer startup did not print SHELL_CONSUMER_STARTUP_OK.'
    }
    if ($startup.Output -match 'SHELL_CONSUMER_STARTUP_FAILED') {
        Write-Host $startup.Output
        throw 'Consumer startup reported SHELL_CONSUMER_STARTUP_FAILED.'
    }
    Write-Host 'SHELL_CONSUMER_STARTUP_OK'
    Write-Host ''
    Write-Host 'Package consumer smoke: OK'
}
finally {
    Remove-OrigoPath $work
}
