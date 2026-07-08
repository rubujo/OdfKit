#Requires -Version 7.0
<#
.SYNOPSIS
    執行 OdsStreamWriter 與 MiniExcel、ClosedXML 之跨套件串流寫入效能對比。
.DESCRIPTION
    以手動計時模式（非 BenchmarkDotNet 統計工作）執行
    CompetitiveStreamWriteBenchmarks 所涵蓋的三個情境：OdsStreamWriter、
    MiniExcel、ClosedXML，各情境於獨立子行程中量測一次 1,000,000 列 x 10
    欄混合型別資料的寫入耗時、GC 累積配置量、峰值工作集與輸出檔案大小。
    完整方法論、授權裁定與結果解讀請見 docs/performance-comparison.md。
.PARAMETER Configuration
    建置組態，預設 Release。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"

Push-Location $repoRoot
try {
    Write-Host "建置 OdfKit.Benchmarks ($Configuration)…"
    dotnet build $benchmarkProject -c $Configuration

    $assemblyPath = Join-Path $repoRoot "OdfKit.Benchmarks/bin/$Configuration/net10.0/OdfKit.Benchmarks.dll"
    if (-not (Test-Path $assemblyPath)) {
        throw "找不到建置產物：$assemblyPath"
    }

    Write-Host ""
    Write-Host "執行跨套件對比（手動計時模式，各情境獨立子行程執行一次）…"
    dotnet $assemblyPath --manual-competitive

    Write-Host ""
    Write-Host "提示：若需要 BenchmarkDotNet 統計工作（較長執行時間），可改用："
    Write-Host "  dotnet run --project OdfKit.Benchmarks -c $Configuration -- --filter *CompetitiveStreamWriteBenchmarks*"
}
finally {
    Pop-Location
}
