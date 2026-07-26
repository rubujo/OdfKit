#Requires -Version 7.0
<#
.SYNOPSIS
驗證 WebFont 規範基準、相依政策與全專案 GitHub Actions 供應鏈政策。
.PARAMETER Online
向 NuGet 與 GitHub 官方 API 查詢最新穩定版本；連線失敗時採 fail closed。
#>
[CmdletBinding()]
param(
    [switch]$Online
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$policyPath = Join-Path $PSScriptRoot "webfont-standards-dependency-policy.json"
$dependencyPolicyPath = Join-Path $PSScriptRoot "webfont-dependency-policy.json"
$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json -Depth 20
$dependencyPolicy = Get-Content -LiteralPath $dependencyPolicyPath -Raw | ConvertFrom-Json -Depth 20

if ($policy.schemaVersion -ne 1) {
    throw "不支援的 WebFont 規範與相依政策版本。"
}
$reviewedAt = [DateTime]::ParseExact(
    [string]$policy.reviewedAt,
    "yyyy-MM-dd",
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
$reviewAge = [DateTime]::UtcNow.Date - $reviewedAt.Date
if ($reviewAge.TotalDays -lt 0 -or $reviewAge.TotalDays -gt [int]$policy.maximumReviewAgeDays) {
    throw "WebFont 規範與相依政策已超過允許的複查期限。"
}

$requiredStandards = @("OpenType", "Unicode", "WOFF", "WOFF2", "CSS-Fonts", "IFT")
$standardsById = @{}
foreach ($standard in $policy.standards) {
    $id = [string]$standard.id
    if ([string]::IsNullOrWhiteSpace($id) -or $standardsById.ContainsKey($id)) {
        throw "WebFont 規範政策含空白或重複識別碼。"
    }
    if ([string]::IsNullOrWhiteSpace([string]$standard.version) `
        -or [string]::IsNullOrWhiteSpace([string]$standard.status) `
        -or -not ([uri]$standard.uri).Scheme.Equals("https", [StringComparison]::OrdinalIgnoreCase)) {
        throw "WebFont 規範政策含不完整或不安全的來源。"
    }
    $standardsById[$id] = $standard
}
foreach ($requiredStandard in $requiredStandards) {
    if (-not $standardsById.ContainsKey($requiredStandard)) {
        throw "WebFont 規範政策缺少必要規範：$requiredStandard"
    }
}
if ([string]$standardsById.OpenType.version -ne "1.9.1" `
    -or [string]::IsNullOrWhiteSpace([string]$standardsById.OpenType.errataUri) `
    -or [string]$standardsById.IFT.version -ne "2025-11-18" `
    -or [string]$standardsById.IFT.status -ne "candidate-recommendation-draft") {
    throw "WebFont OpenType／IFT 規範基準不是目前已稽核版本。"
}

$trackedPackages = @{}
foreach ($package in $policy.latestStablePackages) {
    $id = [string]$package.id
    $version = [string]$package.version
    if ([string]::IsNullOrWhiteSpace($id) -or $trackedPackages.ContainsKey($id) `
        -or $version -notmatch '^\d+(?:\.\d+){2,3}$') {
        throw "WebFont 最新穩定套件政策含無效項目。"
    }
    $trackedPackages[$id] = $version
}

$projectFiles = @(
    Get-ChildItem -LiteralPath $repoRoot -Directory |
        Where-Object { $_.Name -like "OdfKit.WebFonts.*" -or $_.Name -eq "OdfKit.Extensions.Html.WebFonts" } |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter "*.csproj" -File }
)
$packageReferenceFiles = @($projectFiles.FullName) + @(
    Join-Path $PSScriptRoot "OdfKit.WebFonts.Package.props"
)

# Central Package Management：多數 PackageReference 已移除內嵌 Version，改由方案根目錄
# Directory.Packages.props 的 PackageVersion 集中管理。此處先建立 id -> 版本對照，
# 供下方 PackageReference 缺少 Version 屬性時回退查詢，維持本腳本的相依版本稽核能力。
$centralPackageVersionsPath = Join-Path $repoRoot "Directory.Packages.props"
$centralVersionsById = @{}
if (Test-Path -LiteralPath $centralPackageVersionsPath) {
    [xml]$centralPackageVersionsXml = Get-Content -LiteralPath $centralPackageVersionsPath -Raw
    foreach ($packageVersion in $centralPackageVersionsXml.Project.ItemGroup.PackageVersion) {
        $centralId = [string]$packageVersion.Include
        $centralVersion = [string]$packageVersion.Version
        if (-not [string]::IsNullOrWhiteSpace($centralId) -and -not [string]::IsNullOrWhiteSpace($centralVersion)) {
            $centralVersionsById[$centralId] = $centralVersion
        }
    }
}

$declaredVersions = @{}
foreach ($file in $packageReferenceFiles) {
    [xml]$project = Get-Content -LiteralPath $file -Raw
    foreach ($reference in $project.Project.ItemGroup.PackageReference) {
        $id = [string]$reference.Include
        $version = [string]$reference.Version
        if ([string]::IsNullOrWhiteSpace($version) -and -not [string]::IsNullOrWhiteSpace($id)) {
            $version = $centralVersionsById[$id]
        }
        if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($version)) {
            continue
        }
        if ($version.Contains('-')) {
            throw "WebFont 直接相依不得使用 Preview：$id $version"
        }
        if ($declaredVersions.ContainsKey($id) -and $declaredVersions[$id] -ne $version) {
            throw "WebFont 直接相依版本不一致：$id"
        }
        $declaredVersions[$id] = $version
    }
}
foreach ($package in $trackedPackages.GetEnumerator()) {
    if (-not $declaredVersions.ContainsKey($package.Key) -or $declaredVersions[$package.Key] -ne $package.Value) {
        throw "WebFont 直接相依未使用已稽核的最新穩定版本：$($package.Key) $($package.Value)"
    }
}
foreach ($package in $declaredVersions.GetEnumerator()) {
    if (-not $trackedPackages.ContainsKey($package.Key)) {
        throw "WebFont 直接相依未納入最新穩定版政策：$($package.Key) $($package.Value)"
    }
}

$exceptionsById = @{}
foreach ($exception in $policy.prereleaseExceptions) {
    $id = [string]$exception.id
    $reviewBy = [DateTime]::ParseExact(
        [string]$exception.reviewBy,
        "yyyy-MM-dd",
        [Globalization.CultureInfo]::InvariantCulture)
    if ([string]::IsNullOrWhiteSpace($id) -or $exceptionsById.ContainsKey($id) `
        -or -not ([string]$exception.version).Contains('-') `
        -or [string]::IsNullOrWhiteSpace([string]$exception.reason) `
        -or [string]::IsNullOrWhiteSpace([string]$exception.removeWhen) `
        -or $reviewBy.Date -lt [DateTime]::UtcNow.Date) {
        throw "WebFont Preview 例外缺少有效理由、移除條件或複查期限。"
    }
    $exceptionsById[$id] = $exception
}
foreach ($package in $dependencyPolicy.packages) {
    $version = [string]$package.version
    if (-not $version.Contains('-')) {
        continue
    }
    $id = [string]$package.id
    if (-not $exceptionsById.ContainsKey($id) `
        -or [string]$exceptionsById[$id].version -ne $version) {
        throw "WebFont resolved closure 含未核准的 Preview 相依：$id $version"
    }
}
foreach ($exception in $exceptionsById.GetEnumerator()) {
    $resolved = $dependencyPolicy.packages | Where-Object { [string]$_.id -eq $exception.Key }
    if (@($resolved).Count -ne 1 -or [string]$resolved.version -ne [string]$exception.Value.version) {
        throw "WebFont Preview 例外未對應目前 resolved closure：$($exception.Key)"
    }
}

& (Join-Path $PSScriptRoot "Test-GitHubActionsPolicy.ps1") -Online:$Online

if ($Online) {
    foreach ($package in $trackedPackages.GetEnumerator()) {
        $id = $package.Key.ToLowerInvariant()
        $indexUri = "https://api.nuget.org/v3-flatcontainer/$id/index.json"
        $response = Invoke-RestMethod -Uri $indexUri -MaximumRetryCount 3 -RetryIntervalSec 2 -TimeoutSec 60
        $stableVersions = @(
            $response.versions |
                Where-Object { [string]$_ -match '^\d+(?:\.\d+){2,3}$' } |
                ForEach-Object { [version]$_ } |
                Sort-Object -Descending
        )
        if ($stableVersions.Count -eq 0) {
            throw "NuGet 官方資料找不到穩定版本：$($package.Key)"
        }
        $latestStable = $stableVersions[0].ToString()
        if ($latestStable -ne $package.Value) {
            throw "WebFont NuGet 相依不是最新穩定版：$($package.Key) $($package.Value) → $latestStable"
        }
    }

}

$mode = if ($Online) { "官方 NuGet／GitHub 線上" } else { "鎖定政策離線" }
Write-Host "OK：WebFont 規範、相依、GitHub Actions SHA 與 Preview 例外通過（$mode）。"
