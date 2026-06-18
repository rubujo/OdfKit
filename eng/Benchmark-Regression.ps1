#Requires -Version 7.0
<#
.SYNOPSIS
    執行 BenchmarkDotNet 微基準並與基準線比對（PERF-3c）。
.DESCRIPTION
    以短迭代執行 DomInsert 基準，解析 Mean 並與 eng/baselines/performance-baselines.json 比對。
    超過容許回歸比例時以非零結束碼結束（CI 可設 continue-on-error）。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER UpdateBaseline
    以本次量測更新基準線 JSON（僅限本機調整基準時使用）。
.PARAMETER Filter
    BenchmarkDotNet 篩選器，預設 DomInsert。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$UpdateBaseline,
    [string]$Filter = "*DomInsert*"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"
$baselinePath = Join-Path $PSScriptRoot "baselines/performance-baselines.json"
$benchmarkKey = "DomInsertBenchmarks.SequentialInsertAfter"

Push-Location $repoRoot
try {
    Write-Host "執行 BenchmarkDotNet（filter: $Filter）…"
    $output = dotnet run --project $benchmarkProject -c $Configuration -- `
        --filter $Filter `
        --job short `
        --warmupCount 0 `
        --iterationCount 5 `
        2>&1 | Out-String

    Write-Host $output

    if ($output -notmatch '\|\s*SequentialInsertAfter\s*\|\s*([\d.,]+)\s*us\s*\|') {
        throw "無法從 BenchmarkDotNet 輸出解析 SequentialInsertAfter Mean（µs）。"
    }

    $meanUs = [double]$Matches[1].Replace(',', '.')
    $meanNs = [long]($meanUs * 1000)

    if ($UpdateBaseline) {
        $baseline = @{
            version = 1
            benchmarks = @{
                $benchmarkKey = @{
                    meanNanoseconds = $meanNs
                    toleranceRatio = 0.4
                    unit = "us"
                    note = "Updated by Benchmark-Regression.ps1 on $(Get-Date -Format 'yyyy-MM-dd')"
                }
            }
        }
        $baseline | ConvertTo-Json -Depth 5 | Set-Content -Path $baselinePath -Encoding UTF8
        Write-Host "已更新基準線：$benchmarkKey = ${meanUs} µs"
        return
    }

    if (-not (Test-Path $baselinePath)) {
        throw "找不到基準線檔案：$baselinePath"
    }

    $json = Get-Content -Path $baselinePath -Raw | ConvertFrom-Json
    $entry = $json.benchmarks.$benchmarkKey
    if ($null -eq $entry) {
        throw "基準線缺少項目：$benchmarkKey"
    }

    $baselineNs = [long]$entry.meanNanoseconds
    $tolerance = [double]$entry.toleranceRatio
    $maxAllowedNs = [long]($baselineNs * (1 + $tolerance))

    Write-Host ""
    Write-Host "基準比對：$benchmarkKey"
    Write-Host "  基準 Mean：$([math]::Round($baselineNs / 1000.0, 2)) µs"
    Write-Host "  本次 Mean：$([math]::Round($meanNs / 1000.0, 2)) µs"
    Write-Host "  容許上限：$([math]::Round($maxAllowedNs / 1000.0, 2)) µs (+$([math]::Round($tolerance * 100))%)"

    if ($meanNs -gt $maxAllowedNs) {
        Write-Error "效能回歸：本次 $([math]::Round($meanNs / 1000.0, 2)) µs 超過基準上限 $([math]::Round($maxAllowedNs / 1000.0, 2)) µs。"
        exit 1
    }

    Write-Host "通過：未超過回歸門檻。"
}
finally {
    Pop-Location
}