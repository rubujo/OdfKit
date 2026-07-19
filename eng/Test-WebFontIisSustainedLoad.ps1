#Requires -Version 7.0
<#
.SYNOPSIS
以鎖定 CNS 字型在四種 IIS hosting 路徑執行有界持續負載回歸。
#>
[CmdletBinding()]
param(
    [string]$Destination = "artifacts/webfont-iis-sustained-load",

    [string]$CnsFontArchivePath,

    [ValidateRange(1, 256)]
    [int]$Concurrency = 16,

    [ValidateRange(1, 1000000)]
    [int]$MinimumRequestCount = 4096,

    [ValidateRange(1, 1000000)]
    [int]$MaximumRequestCount = 65536,

    [ValidateRange(1, 60)]
    [int]$MinimumDurationSeconds = 30,

    [switch]$ReuseCompletedEvidence
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$destinationPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
$repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $destinationPath.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination 必須位於方案目錄內。"
}
if ($MaximumRequestCount -lt $MinimumRequestCount) {
    throw "MaximumRequestCount 不得小於 MinimumRequestCount。"
}

$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "external-tools.json") -Raw | ConvertFrom-Json
$fontDefinition = $manifest.webFontSmoke.internationalFonts.cnsExtB
$preparationPath = Join-Path $destinationPath "preparation"
$font = Get-ChildItem -LiteralPath (Join-Path $preparationPath "sources") `
    -Filter $fontDefinition.fileName -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $ReuseCompletedEvidence -or $null -eq $font) {
    $smokeArguments = @{
        Destination = [IO.Path]::GetRelativePath($repoRoot, $preparationPath)
    }
    if (-not [string]::IsNullOrWhiteSpace($CnsFontArchivePath)) {
        $smokeArguments.CnsFontArchivePath = $CnsFontArchivePath
    }
    & (Join-Path $PSScriptRoot "Test-WebFontSmoke.ps1") @smokeArguments
    $font = Get-ChildItem -LiteralPath (Join-Path $preparationPath "sources") `
        -Filter $fontDefinition.fileName -File -Recurse | Select-Object -First 1
}
if ($null -eq $font) {
    throw "持續負載準備程序未產生鎖定的 CNS Ext-B 字型。"
}
$sourceSha256 = (Get-FileHash -LiteralPath $font.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceSha256 -ne $fontDefinition.sha256) {
    throw "持續負載使用的 CNS Ext-B 字型 SHA-256 不符合。"
}

$commonArguments = @{
    FontPath = $font.FullName
    SourceSha256 = $sourceSha256
    HostedLoadConcurrency = $Concurrency
    HostedLoadMinimumRequestCount = $MinimumRequestCount
    HostedLoadMaximumRequestCount = $MaximumRequestCount
    HostedLoadMinimumDurationSeconds = $MinimumDurationSeconds
}

function Test-ReusableEvidence {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][int]$ExpectedLoadCount)

    if (-not $ReuseCompletedEvidence -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }
    $evidence = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $loads = if ($ExpectedLoadCount -eq 1) { @($evidence.hostedLoad) } else { @($evidence.models.hostedLoad) }
    return $evidence.sourceSha256 -eq $sourceSha256 -and
        $loads.Count -eq $ExpectedLoadCount -and
        @($loads | Where-Object {
                $_.concurrency -ne $Concurrency -or
                $_.minimumRequestCount -ne $MinimumRequestCount -or
                $_.maximumRequestCount -ne $MaximumRequestCount -or
                $_.minimumDurationSeconds -ne $MinimumDurationSeconds -or
                $_.requestCount -lt $MinimumRequestCount -or
                $_.requestCount -gt $MaximumRequestCount
            }).Count -eq 0
}

$integratedPath = Join-Path $destinationPath "webforms-integrated"
$integratedEvidencePath = Join-Path $integratedPath "evidence.json"
if (-not (Test-ReusableEvidence -Path $integratedEvidencePath -ExpectedLoadCount 1)) {
    & (Join-Path $PSScriptRoot "Test-WebFontIisExpressSmoke.ps1") @commonArguments `
        -Pipeline Integrated `
        -Destination ([IO.Path]::GetRelativePath($repoRoot, $integratedPath))
}

$classicPath = Join-Path $destinationPath "webforms-classic"
$classicEvidencePath = Join-Path $classicPath "evidence.json"
if (-not (Test-ReusableEvidence -Path $classicEvidencePath -ExpectedLoadCount 1)) {
    & (Join-Path $PSScriptRoot "Test-WebFontIisExpressSmoke.ps1") @commonArguments `
        -NoBuild `
        -Pipeline Classic `
        -Destination ([IO.Path]::GetRelativePath($repoRoot, $classicPath))
}

$aspNetCorePath = Join-Path $destinationPath "aspnetcore"
$aspNetCoreEvidencePath = Join-Path $aspNetCorePath "evidence.json"
if (-not (Test-ReusableEvidence -Path $aspNetCoreEvidencePath -ExpectedLoadCount 2)) {
    $aspNetCoreArguments = @{
        Destination = [IO.Path]::GetRelativePath($repoRoot, $aspNetCorePath)
    }
    if ($ReuseCompletedEvidence) { $aspNetCoreArguments.ReuseCompletedEvidence = $true }
    & (Join-Path $PSScriptRoot "Test-WebFontAspNetCoreIisExpressSmoke.ps1") `
        @commonArguments @aspNetCoreArguments
}

$webFormsIntegrated = Get-Content -LiteralPath $integratedEvidencePath -Raw | ConvertFrom-Json
$webFormsClassic = Get-Content -LiteralPath $classicEvidencePath -Raw | ConvertFrom-Json
$aspNetCore = Get-Content -LiteralPath $aspNetCoreEvidencePath -Raw | ConvertFrom-Json
$loads = @(
    [ordered]@{ hostingPath = "SystemWeb-Integrated"; evidence = $webFormsIntegrated.hostedLoad }
    [ordered]@{ hostingPath = "SystemWeb-Classic"; evidence = $webFormsClassic.hostedLoad }
    @($aspNetCore.models | ForEach-Object {
            [ordered]@{ hostingPath = "AspNetCore-$($_.hostingModel)"; evidence = $_.hostedLoad }
        })
)
if ($loads.Count -ne 4 -or @($loads | Where-Object {
            $_.evidence.requestCount -lt $MinimumRequestCount -or
            $_.evidence.requestCount -gt $MaximumRequestCount -or
            $_.evidence.minimumDurationSeconds -ne $MinimumDurationSeconds
        }).Count -ne 0) {
    throw "四種 IIS hosting 路徑的持續負載證據不完整。"
}

[ordered]@{
    schemaVersion = 1
    testKind = "bounded-sustained-load"
    sourceSha256 = $sourceSha256
    concurrency = $Concurrency
    minimumRequestCount = $MinimumRequestCount
    maximumRequestCount = $MaximumRequestCount
    minimumDurationSeconds = $MinimumDurationSeconds
    loads = $loads
} | ConvertTo-Json -Depth 10 | Set-Content `
    -LiteralPath (Join-Path $destinationPath "evidence.json") `
    -Encoding utf8NoBOM

Write-Host "PASS：四種 IIS hosting 路徑的有界持續負載回歸完成。"
