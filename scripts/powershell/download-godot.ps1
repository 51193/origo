# Download the Godot mono binary matching Origo.GodotAdapter's SDK pin.

param(
    [switch]$PrintVersion,
    [string]$ProjectPath = ''
)

. (Join-Path $PSScriptRoot 'common.ps1')

if ($PrintVersion) {
    Write-Output (Get-OrigoGodotSdkVersion -ProjectPath $ProjectPath)
    exit 0
}

$binary = Get-OrigoGodotBinary
Write-Output $binary
