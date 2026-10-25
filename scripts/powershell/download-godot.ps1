# Download the Godot mono binary matching Origo.GodotAdapter's SDK pin.

param(
    [switch]$PrintVersion
)

. (Join-Path $PSScriptRoot 'common.ps1')

if ($PrintVersion) {
    Write-Output (Get-OrigoGodotSdkVersion)
    exit 0
}

$binary = Get-OrigoGodotBinary
Write-Output $binary
