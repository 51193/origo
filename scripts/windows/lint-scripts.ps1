# Lint the Windows PowerShell development scripts and shared documentation.

. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-OrigoRoot
$failed = $false
$files = Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter '*.ps1'
foreach ($file in $files) {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0) {
        $failed = $true
        Write-Host ('PowerShell parse errors in ' + $file.FullName + ':')
        foreach ($error in $errors) {
            Write-Host ('  line ' + $error.Extent.StartLineNumber + ': ' + $error.Message)
        }
    }
}
if ($failed) {
    throw 'PowerShell syntax check failed.'
}
Write-Host ('PowerShell syntax OK (' + $files.Count + ' scripts).')

Invoke-OrigoPython -Arguments @('scripts\check_test.py')
Invoke-OrigoPython -Arguments @('scripts\validate-agent-docs.py')
Write-Host 'Windows script lint: OK'
