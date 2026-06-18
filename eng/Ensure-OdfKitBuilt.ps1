#Requires -Version 7.0
<#
.SYNOPSIS
    確保 OdfKit net10.0 組件已建置且與來源同步。
.DESCRIPTION
    比對 OdfKit.dll 與手寫來源檔時間戳；若過期則僅建置 net10.0（不編譯 netstandard2.0）。
    若先前建置被中止，會先關閉編譯器伺服器並清除損壞的 net10.0 obj 中繼資料。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Force
    強制重編 net10.0，即使時間戳顯示為最新。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$odfKitProject = Join-Path $repoRoot "OdfKit/OdfKit.csproj"
$odfKitDll = Join-Path $repoRoot "OdfKit/bin/$Configuration/net10.0/OdfKit.dll"
$odfKitObj = Join-Path $repoRoot "OdfKit/obj/$Configuration/net10.0"

function Get-LatestOdfKitSourceWriteTime {
    $latest = Get-ChildItem (Join-Path $repoRoot "OdfKit") -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    return $latest.LastWriteTime
}

$needsBuild = $Force -or -not (Test-Path $odfKitDll)
if (-not $needsBuild) {
    $dllTime = (Get-Item $odfKitDll).LastWriteTime
    $srcTime = Get-LatestOdfKitSourceWriteTime
    $needsBuild = $srcTime -gt $dllTime
    if ($needsBuild) {
        Write-Host "OdfKit.dll 過期（dll=$dllTime，最新來源=$srcTime）。"
    }
}

if (-not $needsBuild) {
    Write-Host "OdfKit net10.0 已為最新，略過建置。"
    return
}

Write-Host "建置 OdfKit net10.0 ($Configuration)…"
dotnet build-server shutdown 2>&1 | Out-Null

if (Test-Path $odfKitObj) {
    Remove-Item -Recurse -Force $odfKitObj
    Write-Host "已清除損壞的 OdfKit obj/$Configuration/net10.0 中繼資料。"
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build $odfKitProject -c $Configuration -f net10.0 --no-restore -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "OdfKit 建置失敗，結束碼 $LASTEXITCODE"
}
$sw.Stop()

Write-Host "OdfKit 建置完成，耗時 $([math]::Round($sw.Elapsed.TotalSeconds, 1)) 秒。"