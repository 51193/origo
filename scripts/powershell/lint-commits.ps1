# Conventional Commit lint for Windows, mirroring scripts/bash/lint-commits.sh.

param(
    [string]$Base = '',
    [string]$Head = 'HEAD'
)

. (Join-Path $PSScriptRoot 'common.ps1')

function Get-OrigoGitText {
    param([string[]]$Arguments)
    $result = Invoke-OrigoNative -FilePath 'git' -Arguments $Arguments -AllowFailure
    return $result
}

if ([string]::IsNullOrWhiteSpace($Base)) {
    $mergeBase = Get-OrigoGitText @('merge-base', 'origin/main', 'HEAD')
    if ($mergeBase.ExitCode -eq 0) {
        $Base = $mergeBase.Output.Trim()
    }
    else {
        $parent = Get-OrigoGitText @('rev-parse', '--verify', 'HEAD~1')
        if ($parent.ExitCode -ne 0) {
            throw 'Cannot determine a base commit (origin/main unavailable and HEAD has no parent).'
        }
        $Base = 'HEAD~1'
        Write-Host 'WARNING: origin/main is unavailable; linting HEAD~1..HEAD only.'
    }
}

$commitsResult = Get-OrigoGitText @('rev-list', '--no-merges', ($Base + '..' + $Head))
if ($commitsResult.ExitCode -ne 0) {
    throw $commitsResult.Output.Trim()
}
$commits = @($commitsResult.Output -split "`r?`n" | Where-Object { $_.Trim() -ne '' } | ForEach-Object { $_.Trim() })
$failed = $false
foreach ($sha in $commits) {
    $authorName = (Get-OrigoGitText @('log', '-1', '--pretty=format:%an', $sha)).Output.Trim()
    $authorEmail = (Get-OrigoGitText @('log', '-1', '--pretty=format:%ae', $sha)).Output.Trim()
    if ($authorName -eq 'dependabot[bot]' -or $authorEmail -like '*dependabot[bot]@users.noreply.github.com') {
        Write-Host ('SKIP Dependabot-authored commit ' + $sha)
        continue
    }

    $subject = (Get-OrigoGitText @('log', '-1', '--pretty=format:%s', $sha)).Output.Trim()
    if ([string]::IsNullOrWhiteSpace($subject)) {
        Write-Host ('FAIL empty subject (' + $sha + ')')
        $failed = $true
    }
    elseif ($subject.Length -gt 72) {
        Write-Host ('FAIL subject longer than 72 chars: ' + $subject)
        $failed = $true
    }
    elseif ($subject.EndsWith('.')) {
        Write-Host ('FAIL subject must not end with a period: ' + $subject)
        $failed = $true
    }
    elseif ($subject -notmatch '^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([^)]*\))?!?: .+') {
        Write-Host ('FAIL non-conventional subject: ' + $subject)
        $failed = $true
    }

    $body = (Get-OrigoGitText @('log', '-1', '--pretty=format:%b', $sha)).Output
    foreach ($line in ($body -split "`r?`n")) {
        if ($line.Length -gt 72) {
            Write-Host ('FAIL body line longer than 72 chars in ' + $sha + ' (''' + $subject + '''): ' + $line)
            $failed = $true
        }
    }
}

if ($failed) {
    throw 'Commit messages must follow Conventional Commits with <=72-character lines.'
}
Write-Host 'Commit lint: OK'
