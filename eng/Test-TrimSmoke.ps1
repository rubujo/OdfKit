#Requires -Version 7.0
<#
.SYNOPSIS
    建置並執行 OdfKit trimming 煙霧測試（PERF-5e）。
.DESCRIPTION
    先以 eng/Ensure-OdfKitBuilt.ps1 確保 OdfKit net10.0 已建置（僅單一 TFM，避免 netstandard2.0 連帶編譯）。
    再以 PublishTrimmed + SelfContained 發佈 TrimSmoke；-r win-x64 僅在 publish 階段指定，避免日常 build 強制 RID 重編。
    裁剪分析警告僅輸出供審查，不視為失敗。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER PublishAot
    以 Native AOT 發佈並執行原生可執行檔；任何發佈或執行錯誤皆視為失敗。
.PARAMETER ForceRebuildOdfKit
    強制重編 OdfKit net10.0。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$PublishAot,
    [switch]$ForceRebuildOdfKit
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tools/OdfKit.TrimSmoke/OdfKit.TrimSmoke.csproj"
$ensureScript = Join-Path $repoRoot "eng/Ensure-OdfKitBuilt.ps1"
$publishDir = if ($PublishAot) {
    Join-Path $repoRoot "artifacts/trim-smoke-aot"
} else {
    Join-Path $repoRoot "artifacts/trim-smoke"
}

Push-Location $repoRoot
try {
    $ensureArgs = @{ Configuration = $Configuration }
    if ($ForceRebuildOdfKit) { $ensureArgs.Force = $true }
    & $ensureScript @ensureArgs

    if (Test-Path $publishDir) {
        Remove-Item -Recurse -Force $publishDir
    }

    $modeLabel = if ($PublishAot) { "PublishTrimmed + NativeAOT" } else { "PublishTrimmed" }
    Write-Host "$modeLabel 發佈 TrimSmoke ($Configuration / win-x64)…"

    $restoreArgs = @("restore", $project, "-r", "win-x64")
    if ($PublishAot) {
        $restoreArgs += "/p:PublishAot=true"
    }
    dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) {
        throw "TrimSmoke 還原失敗，結束碼 $LASTEXITCODE"
    }

    $publishArgs = @(
        "publish", $project,
        "-c", $Configuration,
        "-f", "net10.0",
        "-r", "win-x64",
        "-o", $publishDir,
        "--no-restore",
        "-p:RunAnalyzers=false"
    )
    if ($PublishAot) {
        $publishArgs += "/p:PublishAot=true"
    }
    else {
        $publishArgs += "-p:BuildProjectReferences=false"
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet @publishArgs 2>&1 | ForEach-Object {
        Write-Host $_
        if ($_ -match 'IL[23]\d{4}') {
            Write-Warning $_
        }
    }
    $sw.Stop()
    Write-Host "發佈耗時：$([math]::Round($sw.Elapsed.TotalSeconds, 1)) 秒"

    if ($LASTEXITCODE -ne 0) {
        throw "TrimSmoke 建置失敗，結束碼 $LASTEXITCODE"
    }

    $exeName = if ($IsWindows) { "OdfKit.TrimSmoke.exe" } else { "OdfKit.TrimSmoke" }
    $exe = Join-Path $publishDir $exeName
    if (-not (Test-Path $exe)) {
        throw "找不到裁剪後執行檔：$exe"
    }

    Write-Host "執行 TrimSmoke…"
    & $exe
    if ($LASTEXITCODE -ne 0) {
        throw "TrimSmoke 執行失敗，結束碼 $LASTEXITCODE"
    }

    Write-Host "TrimSmoke 通過（$modeLabel）。"
}
finally {
    Pop-Location
}
