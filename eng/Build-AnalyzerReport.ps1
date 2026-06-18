#Requires -Version 7.0
<#
.SYNOPSIS
    產生 OdfKit 建置 binlog 供 Analyzer Summary 剖析（本機診斷用）。
.DESCRIPTION
    以 ReportAnalyzer + binary logger 建置 OdfKit net10.0，輸出至 artifacts/analyzer-report/*.binlog。
    請以 MSBuild Structured Log Viewer（https://msbuildlog.com/）開啟，檢視最耗時的 CA 規則。
    參考：https://www.meziantou.net/understanding-the-impact-of-roslyn-analyzers-on-the-build-time.htm
.PARAMETER Configuration
    建置組態，預設 Release。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "OdfKit/OdfKit.csproj"
$outDir = Join-Path $repoRoot "artifacts/analyzer-report"
$binlog = Join-Path $outDir "OdfKit-net10.0-$Configuration.binlog"

Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    if (Test-Path $binlog) {
        Remove-Item -Force $binlog
    }

    Write-Host "建置 OdfKit（net10.0 / $Configuration / RunAnalyzers=true / ReportAnalyzer=true）…"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet build $project `
        -c $Configuration `
        -f net10.0 `
        --no-restore `
        -v minimal `
        -p:RunAnalyzersDuringBuild=true `
        -p:ReportAnalyzer=true `
        "-bl:$binlog"
    if ($LASTEXITCODE -ne 0) {
        throw "建置失敗，結束碼 $LASTEXITCODE"
    }
    $sw.Stop()

    Write-Host "完成，耗時 $([math]::Round($sw.Elapsed.TotalSeconds, 1)) 秒。"
    Write-Host "Binlog：$binlog"
    Write-Host "請以 https://msbuildlog.com/ 開啟，展開 Analyzer Summary 檢視各規則耗時。"
    Write-Host "注意：binlog 可能含環境變數，請勿公開分享。"
}
finally {
    Pop-Location
}