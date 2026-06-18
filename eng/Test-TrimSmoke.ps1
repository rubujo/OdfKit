#Requires -Version 7.0
<#
.SYNOPSIS
    建置並執行 OdfKit trimming 煙霧測試（PERF-5e）。
.DESCRIPTION
    以 PublishTrimmed 建置 tools/OdfKit.TrimSmoke，驗證主要 API 根在裁剪後仍可執行。
    裁剪分析警告僅輸出供審查，不視為失敗（BouncyCastle 等動態路徑尚未完全 AOT 化）。
.PARAMETER Configuration
    建置組態，預設 Release。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tools/OdfKit.TrimSmoke/OdfKit.TrimSmoke.csproj"
$publishDir = Join-Path $repoRoot "artifacts/trim-smoke"

Push-Location $repoRoot
try {
    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }

    Write-Host "PublishTrimmed 建置 TrimSmoke ($Configuration)…"
    dotnet publish $project -c $Configuration -o $publishDir 2>&1 | ForEach-Object {
        Write-Host $_
        if ($_ -match 'IL2\d{4}') {
            Write-Warning $_
        }
    }

    $exe = Join-Path $publishDir "OdfKit.TrimSmoke.exe"
    if (-not (Test-Path $exe)) {
        throw "找不到裁剪後執行檔：$exe"
    }

    Write-Host "執行 TrimSmoke…"
    & $exe
    if ($LASTEXITCODE -ne 0) {
        throw "TrimSmoke 執行失敗，結束碼 $LASTEXITCODE"
    }

    Write-Host "TrimSmoke 通過。"
}
finally {
    Pop-Location
}