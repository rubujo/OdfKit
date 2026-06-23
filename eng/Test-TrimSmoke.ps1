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
    額外以 Native AOT 發佈；若因反射／密碼學路徑失敗，僅記錄警告。
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

    dotnet restore $project

    $publishArgs = @(
        "publish", $project,
        "-c", $Configuration,
        "-f", "net10.0",
        "-o", $publishDir,
        "--no-restore",
        "-p:BuildProjectReferences=false",
        "-p:RunAnalyzers=false"
    )
    if ($PublishAot) {
        $publishArgs += "/p:PublishAot=true"
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet @publishArgs 2>&1 | ForEach-Object {
        Write-Host $_
        if ($_ -match 'IL2\d{4}') {
            Write-Warning $_
        }
    }
    $sw.Stop()
    Write-Host "發佈耗時：$([math]::Round($sw.Elapsed.TotalSeconds, 1)) 秒"

    if ($LASTEXITCODE -ne 0) {
        if ($PublishAot) {
            Write-Warning "Native AOT 發佈失敗（已知限制：BouncyCastle／反射後備路徑）。"
            return
        }

        throw "TrimSmoke 建置失敗，結束碼 $LASTEXITCODE"
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

    Write-Host "TrimSmoke 通過（$modeLabel）。"
}
finally {
    Pop-Location
}