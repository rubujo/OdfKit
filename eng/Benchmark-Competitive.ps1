#Requires -Version 7.0
<#
.SYNOPSIS
    執行 OdfKit 與 MiniExcel、ClosedXML 之跨套件串流寫入與讀取效能對比。
.DESCRIPTION
    以手動計時模式（非 BenchmarkDotNet 統計工作）執行跨套件對比，各情境於獨立子行程中量測一次
    1,000,000 列 x 10 欄混合型別資料：
    - 寫入（CompetitiveStreamWriteBenchmarks 涵蓋的三個情境）：OdsStreamWriter、MiniExcel、
      ClosedXML，量測寫入耗時、GC 累積配置量、峰值工作集與輸出檔案大小。
    - 讀取（CompetitiveStreamReadBenchmarks 涵蓋的三個情境）：OdsStreamReader、MiniExcel、
      ClosedXML，量測讀取耗時、GC 累積配置量與峰值工作集，並以檢查碼驗證讀回的內容與產生器一致；
      檢查碼不符時指令碼失敗，該次量測無效。
    完整方法論、授權裁定與結果解讀請見 docs/performance-comparison.md。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Mode
    Write 只執行寫入對比；Read 只執行讀取對比；All（預設）兩者都執行。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [ValidateSet("Write", "Read", "All")]
    [string]$Mode = "All"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"

Push-Location $repoRoot
try {
    Write-Host "建置 OdfKit.Benchmarks ($Configuration)…"
    dotnet build $benchmarkProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "OdfKit.Benchmarks 建置失敗（exit code $LASTEXITCODE）"
    }

    $assemblyPath = Join-Path $repoRoot "OdfKit.Benchmarks/bin/$Configuration/net10.0/OdfKit.Benchmarks.dll"
    if (-not (Test-Path $assemblyPath)) {
        throw "找不到建置產物：$assemblyPath"
    }

    if ($Mode -in @("Write", "All")) {
        Write-Host ""
        Write-Host "執行跨套件寫入對比（手動計時模式，各情境獨立子行程執行一次）…"
        dotnet $assemblyPath --manual-competitive
        if ($LASTEXITCODE -ne 0) {
            throw "跨套件寫入效能對比執行失敗（exit code $LASTEXITCODE）"
        }
    }

    if ($Mode -in @("Read", "All")) {
        Write-Host ""
        Write-Host "執行跨套件讀取對比（手動計時模式，各情境獨立子行程執行一次，含內容檢查碼）…"
        dotnet $assemblyPath --manual-competitive-read
        if ($LASTEXITCODE -ne 0) {
            throw "跨套件讀取效能對比執行失敗（exit code $LASTEXITCODE）"
        }
    }

    Write-Host ""
    Write-Host "提示：若需要 BenchmarkDotNet 統計工作（較長執行時間），可改用："
    Write-Host "  dotnet run --project OdfKit.Benchmarks -c $Configuration -- --filter *CompetitiveStreamWriteBenchmarks*"
    Write-Host "  dotnet run --project OdfKit.Benchmarks -c $Configuration -- --filter *CompetitiveStreamReadBenchmarks*"
}
finally {
    Pop-Location
}
