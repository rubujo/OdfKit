#Requires -Version 7.0
<#
.SYNOPSIS
驗證 GitHub Actions 的 cache、artifact、排程與 job 資源治理契約。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot "ci-resource-policy.json") -Raw |
    ConvertFrom-Json
if ($policy.schemaVersion -ne 1 -or
    $policy.githubDefaultCacheLimitBytes -ne 10GB -or
    $policy.cacheSoftTargetBytes -le 0 -or
    $policy.cacheSoftTargetBytes -ge $policy.githubDefaultCacheLimitBytes) {
    throw "CI resource policy 的 schema 或 cache 預算不正確。"
}

$workflowRoot = Join-Path $repoRoot ".github/workflows"
$workflowFiles = @(Get-ChildItem -LiteralPath $workflowRoot -Filter "*.yml" -File)
$cacheActionPath = Join-Path $repoRoot ".github/actions/cache-odfkit/action.yml"
$cacheAction = Get-Content -LiteralPath $cacheActionPath -Raw
if (-not $cacheAction.Contains("actions/cache/restore@v6", [StringComparison]::Ordinal) -or
    -not $cacheAction.Contains("github.event_name == 'pull_request'", [StringComparison]::Ordinal) -or
    -not $cacheAction.Contains("github.event_name != 'pull_request'", [StringComparison]::Ordinal)) {
    throw "共用 cache action 未維持 PR 僅還原、受信任分支才儲存的契約。"
}

$setupActionPath = Join-Path $repoRoot ".github/actions/setup-dotnet-odfkit/action.yml"
$setupAction = Get-Content -LiteralPath $setupActionPath -Raw
if (-not $setupAction.Contains(
        'key: nuget-${{ runner.os }}-${{ inputs.nuget-cache-revision }}',
        [StringComparison]::Ordinal) -or
    $setupAction.Contains("nuget-fingerprint", [StringComparison]::Ordinal)) {
    throw "NuGet cache 必須使用明確 epoch 的穩定 OS key，不得按架構或提交內容複製完整 cache。"
}

foreach ($file in $workflowFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text.Contains("uses: actions/cache@", [StringComparison]::Ordinal) -or
        $text.Contains("uses: actions/cache/restore@", [StringComparison]::Ordinal) -or
        $text.Contains("uses: actions/cache/save@", [StringComparison]::Ordinal)) {
        throw "$($file.Name) 必須透過共用 cache-odfkit action，避免 PR 建立 branch-scoped cache。"
    }
    if ($text -match '(?m)^\s*key:.*(?:github\.sha|hashFiles\()') {
        throw "$($file.Name) 的 cache key 不得按 commit 或整檔 hash 無界增生。"
    }

    $lines = Get-Content -LiteralPath $file.FullName
    $insideJobs = $false
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -eq "jobs:") {
            $insideJobs = $true
            continue
        }
        if ($insideJobs -and $line -match '^  ([a-zA-Z0-9_-]+):\s*$') {
            $jobName = $Matches[1]
            $jobLines = [Collections.Generic.List[string]]::new()
            for ($jobIndex = $index + 1; $jobIndex -lt $lines.Count; $jobIndex++) {
                if ($lines[$jobIndex] -match '^  [a-zA-Z0-9_-]+:\s*$') { break }
                $jobLines.Add($lines[$jobIndex])
            }
            $timeoutLine = $jobLines | Where-Object { $_ -match '^    timeout-minutes:\s*(\d+)\s*$' } |
                Select-Object -First 1
            if ($null -eq $timeoutLine) {
                throw "$($file.Name) 的 job '$jobName' 缺少 timeout-minutes。"
            }
            $timeout = [int]([regex]::Match($timeoutLine, '\d+').Value)
            if ($timeout -gt $policy.maxJobTimeoutMinutes) {
                throw "$($file.Name) 的 job '$jobName' timeout 超過政策上限。"
            }
        }

        if ($line -notmatch '^\s+uses:\s+actions/upload-artifact@') { continue }
        $indent = $line.Length - $line.TrimStart().Length
        $retention = $null
        for ($stepIndex = $index + 1; $stepIndex -lt $lines.Count; $stepIndex++) {
            $candidate = $lines[$stepIndex]
            $candidateIndent = $candidate.Length - $candidate.TrimStart().Length
            if ($candidateIndent -le $indent -and $candidate.Trim().StartsWith("- ", [StringComparison]::Ordinal)) {
                break
            }
            if ($candidate -match '^\s+retention-days:\s*(\d+)\s*$') {
                $retention = [int]$Matches[1]
                break
            }
        }
        if ($null -eq $retention -or $retention -gt $policy.maxArtifactRetentionDays) {
            throw "$($file.Name) 的 upload-artifact 缺少短期 retention 或超過政策上限。"
        }
    }
}

$scheduledFiles = @($workflowFiles | Where-Object {
        (Get-Content -LiteralPath $_.FullName -Raw) -match '(?m)^\s{2}schedule:\s*$'
    })
if ($scheduledFiles.Count -gt $policy.maxScheduledWorkflowCount) {
    throw "自動排程 workflow 數量超過政策上限。"
}
foreach ($file in $scheduledFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if (-not $text.Contains("cancel-in-progress: true", [StringComparison]::Ordinal)) {
        throw "$($file.Name) 的排程 workflow 必須防止重疊執行。"
    }
}

$ciText = Get-Content -LiteralPath (Join-Path $workflowRoot "ci.yml") -Raw
if (-not $ciText.Contains("os: [windows-latest]", [StringComparison]::Ordinal) -or
    ([regex]::Matches($ciText, '(?m)^\s+if:\s+failure\(\)\s*$').Count -lt 2)) {
    throw "主 CI 必須避免 Ubuntu smoke 與完整回歸重複，且 diagnostics 只在失敗時上傳。"
}

$performanceText = Get-Content -LiteralPath (Join-Path $workflowRoot "performance-benchmark.yml") -Raw
foreach ($condition in @(
        "inputs.run_webfont_iis_sustained_load",
        "inputs.run_macos_informational")) {
    if (-not $performanceText.Contains($condition, [StringComparison]::Ordinal)) {
        throw "高成本或 informational performance job 必須維持明確手動 opt-in。"
    }
}

$libreOfficeText = Get-Content -LiteralPath (Join-Path $workflowRoot "libreoffice-interop.yml") -Raw
if ($libreOfficeText.Contains("matrix:", [StringComparison]::Ordinal) -or
    -not $libreOfficeText.Contains("libreoffice-msi-", [StringComparison]::Ordinal) -or
    -not $libreOfficeText.Contains("-Framework net8.0", [StringComparison]::Ordinal) -or
    -not $libreOfficeText.Contains("-Framework net10.0", [StringComparison]::Ordinal)) {
    throw "LibreOffice 排程必須單次安裝並依序驗證兩個 TFM。"
}

Write-Host "OK：CI cache、artifact、排程與 job 資源治理契約通過。"
