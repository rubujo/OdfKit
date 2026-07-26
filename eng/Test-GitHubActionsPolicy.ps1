#Requires -Version 7.0
<#
.SYNOPSIS
驗證所有遠端 GitHub Actions 使用不可變 commit SHA，並可線上確認仍指向官方最新版。
.PARAMETER Online
向 GitHub API 驗證版本註解、release tag（CodeQL 使用浮動 major tag）與 SHA 一致。
#>
[CmdletBinding()]
param(
    [switch]$Online
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$githubRoot = Join-Path $repoRoot ".github"
$issues = [Collections.Generic.List[string]]::new()
$usedActions = [Collections.Generic.Dictionary[string, object]]::new(
    [StringComparer]::OrdinalIgnoreCase)

$actionPattern = [regex]::new(
    'uses:\s*(?<repo>[a-z0-9._-]+/[a-z0-9._-]+)(?:/[a-z0-9._-]+)?@(?<ref>[^\s#]+)(?:\s+#\s+(?<version>\S+))?',
    [Text.RegularExpressions.RegexOptions]::IgnoreCase)
foreach ($file in Get-ChildItem -LiteralPath $githubRoot -Recurse -File -Include "*.yml", "*.yaml") {
    $relativePath = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
    foreach ($match in $actionPattern.Matches((Get-Content -LiteralPath $file.FullName -Raw))) {
        $actionRepo = $match.Groups["repo"].Value.ToLowerInvariant()
        $reference = $match.Groups["ref"].Value
        $version = $match.Groups["version"].Value
        if ($reference -notmatch '^[0-9a-f]{40}$') {
            $issues.Add("$relativePath 使用可變 Action ref：$actionRepo@$reference")
            continue
        }
        if ($version -notmatch '^v\d+(?:\.\d+\.\d+)?$') {
            $issues.Add("$relativePath 的 $actionRepo@$reference 缺少可稽核版本註解。")
            continue
        }

        if ($usedActions.ContainsKey($actionRepo)) {
            $existing = $usedActions[$actionRepo]
            if ($existing.Reference -ne $reference -or $existing.Version -ne $version) {
                $issues.Add("$actionRepo 在不同 workflow 使用不一致的 SHA 或版本註解。")
            }
        }
        else {
            $usedActions.Add($actionRepo, [pscustomobject]@{
                    Reference = $reference
                    Version = $version
                })
        }
    }
}

if ($usedActions.Count -eq 0) {
    $issues.Add("未找到任何遠端 GitHub Action，安全性掃描可能已失效。")
}

$dependabot = Get-Content -LiteralPath (Join-Path $githubRoot "dependabot.yml") -Raw
if ($dependabot -notmatch 'package-ecosystem:\s*["'']?github-actions["'']?' `
    -or $dependabot -notmatch '(?s)package-ecosystem:\s*["'']?github-actions["'']?.*?interval:\s*["'']?weekly["'']?') {
    $issues.Add("Dependabot 必須每週更新 SHA-pinned GitHub Actions。")
}

if ($Online -and $issues.Count -eq 0) {
    $headers = @{
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        $headers.Authorization = "Bearer $($env:GITHUB_TOKEN)"
    }

    foreach ($entry in $usedActions.GetEnumerator()) {
        $actionRepo = $entry.Key
        $expected = $entry.Value
        if ($actionRepo -eq "github/codeql-action") {
            if ($expected.Version -notmatch '^v(?<major>\d+)\.\d+\.\d+$') {
                $issues.Add("$actionRepo 必須以完整穩定版本註解標示。")
                continue
            }
            $major = $Matches.major
            $releases = Invoke-RestMethod `
                -Uri "https://api.github.com/repos/$actionRepo/releases?per_page=100" `
                -Headers $headers `
                -MaximumRetryCount 3 `
                -RetryIntervalSec 2 `
                -TimeoutSec 60
            $latestTag = [string](
                $releases |
                    Where-Object { [string]$_.tag_name -match "^v$major\.\d+\.\d+$" } |
                    Sort-Object published_at -Descending |
                    Select-Object -First 1).tag_name
            if ($latestTag -ne $expected.Version) {
                $issues.Add("$actionRepo 不是官方最新穩定 v$major release：$($expected.Version) → $latestTag")
                continue
            }
        }
        elseif ($expected.Version -match '^v\d+$') {
            $latestTag = $expected.Version
        }
        else {
            $release = Invoke-RestMethod `
                -Uri "https://api.github.com/repos/$actionRepo/releases/latest" `
                -Headers $headers `
                -MaximumRetryCount 3 `
                -RetryIntervalSec 2 `
                -TimeoutSec 60
            $latestTag = [string]$release.tag_name
            if ($latestTag -ne $expected.Version) {
                $issues.Add("$actionRepo 不是官方最新穩定 release：$($expected.Version) → $latestTag")
                continue
            }
        }

        $commit = Invoke-RestMethod `
            -Uri "https://api.github.com/repos/$actionRepo/commits/$latestTag" `
            -Headers $headers `
            -MaximumRetryCount 3 `
            -RetryIntervalSec 2 `
            -TimeoutSec 60
        if ([string]$commit.sha -ne $expected.Reference) {
            $issues.Add("$actionRepo@$latestTag 的 pinned SHA 已漂移：$($expected.Reference) → $($commit.sha)")
        }
    }
}

if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Host "  * $_" }
    throw "GitHub Actions 政策驗證失敗：$($issues.Count) 個問題。"
}

$mode = if ($Online) { "官方 GitHub API 線上" } else { "離線" }
Write-Host "OK：$($usedActions.Count) 個 GitHub Actions 均以完整 SHA pinning，並由 Dependabot 每週追蹤最新版（$mode）。"
