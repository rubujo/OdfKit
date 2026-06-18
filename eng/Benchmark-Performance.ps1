#Requires -Version 7.0
<#
.SYNOPSIS
    執行 OdfKit 效能相關單元測試與簡易計時（PERF-3）。
.DESCRIPTION
    執行 DOM 效能、公式剖析與封裝載入相關測試，並輸出總耗時供本機比較。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Framework
    測試目標框架，預設 net8.0。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "OdfKit.Tests/OdfKit.Tests.csproj"
$filter = @(
    "FullyQualifiedName~OdfNodePerformanceTests",
    "FullyQualifiedName~FormulaParserTests",
    "FullyQualifiedName~OdfPackageUnknownEntryTests"
) -join "|"

Push-Location $repoRoot
try {
    Write-Host "建置 OdfKit.Tests ($Configuration / $Framework)…"
    dotnet build $testProject -c $Configuration --framework $Framework

    Write-Host "執行效能相關測試（filter: $filter）…"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet test $testProject -c $Configuration --framework $Framework --filter $filter --no-restore --no-build
    $sw.Stop()

    Write-Host ""
    Write-Host "效能測試子集耗時：$([math]::Round($sw.Elapsed.TotalSeconds, 2)) 秒"
    Write-Host ""
    Write-Host "提示：以 Release 組態重複執行可比較 PERF 變更前後差異。"
    Write-Host "BenchmarkDotNet 微基準："
    Write-Host "  dotnet run --project OdfKit.Benchmarks -c $Configuration"
    Write-Host "基準回歸比對（PERF-3c）："
    Write-Host "  pwsh eng/Benchmark-Regression.ps1 -Configuration $Configuration"
    Write-Host "Trim 煙霧測試（PERF-5e）："
    Write-Host "  pwsh eng/Test-TrimSmoke.ps1 -Configuration $Configuration"
    Write-Host "快速煙霧（單次迭代）："
    Write-Host "  dotnet run --project OdfKit.Benchmarks -c $Configuration -- --filter *FormulaParse* --job short --warmupCount 0 --iterationCount 1"
}
finally {
    Pop-Location
}