#Requires -Version 7.0
<#
.SYNOPSIS
    下載並驗證全字庫（CNS 11643 open data）中文碼對照表，供 baseline 測試使用。
.DESCRIPTION
    依 eng/external-tools.json 的 cns11643MappingTables 釘選資訊下載 MapingTables.zip，
    驗證 SHA-256 後解壓至目的目錄。資料採「政府資料開放授權條款－第 1 版」釋出；
    倉庫不內建對照表資料，僅在測試時下載。輸出解壓根目錄路徑（含 Unicode/、Big5/ 子目錄）。
.PARAMETER DestinationRoot
    快取根目錄；壓縮檔與解壓內容都放在此目錄下。
.PARAMETER ManifestPath
    釘選資訊 JSON 路徑；預設 eng/external-tools.json。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationRoot,

    [string]$ManifestPath = "eng/external-tools.json"
)

$ErrorActionPreference = "Stop"

function Test-ExpectedHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedSha256
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    return [string]::Equals($actual, $ExpectedSha256, [StringComparison]::OrdinalIgnoreCase)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedManifest = if ([System.IO.Path]::IsPathRooted($ManifestPath)) {
    $ManifestPath
}
else {
    Join-Path $repoRoot $ManifestPath
}

$tool = (Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json).cns11643MappingTables
$archivePath = Join-Path $DestinationRoot $tool.archiveFileName
$extractRoot = Join-Path $DestinationRoot "tables"
$unicodeDir = Join-Path $extractRoot "Unicode"

if (-not (Test-Path -LiteralPath $DestinationRoot)) {
    New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null
}

if (-not (Test-ExpectedHash -Path $archivePath -ExpectedSha256 $tool.sha256)) {
    Write-Host "下載全字庫對照表 $($tool.version)：$($tool.uri)"
    Invoke-WebRequest -Uri $tool.uri -OutFile $archivePath -MaximumRetryCount 3 -RetryIntervalSec 5

    if (-not (Test-ExpectedHash -Path $archivePath -ExpectedSha256 $tool.sha256)) {
        $actual = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        throw "全字庫對照表 SHA-256 不符：預期 $($tool.sha256)，實際 $actual。上游可能已改版，請重新釘選 eng/external-tools.json。"
    }
}

if (-not (Test-Path -LiteralPath $unicodeDir -PathType Container)) {
    if (Test-Path -LiteralPath $extractRoot) {
        Remove-Item -LiteralPath $extractRoot -Recurse -Force
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
}

if (-not (Test-Path -LiteralPath $unicodeDir -PathType Container)) {
    throw "解壓後缺少 Unicode/ 目錄：$extractRoot（壓縮檔結構可能已改變）。"
}

Write-Host "PASS：全字庫對照表 $($tool.version) 已就緒（$extractRoot）。"
$extractRoot
