#Requires -Version 7.0
<#
.SYNOPSIS
    將已驗證的 NuGet 套件附加至 GitHub Release（REL-1 發佈流程）。
.DESCRIPTION
    本專案目前不以 nuget.org 為發佈目標；套件以 GitHub Release 資產形式提供，
    供下載後以本機 NuGet feed 或原始碼參照使用。
    預設為乾跑；使用 -CreateRelease 與已登入的 GitHub CLI (gh) 才會建立 Release。
.PARAMETER Tag
    Git 標籤；未指定時依套件版本自動產生（例如 v0.0.1）。
.PARAMETER Title
    Release 標題；未指定時為 OdfKit {版本}。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER CreateRelease
    透過 gh release create 建立 Release 並上傳資產。
.PARAMETER SkipValidation
    略過 Test-NuGetPack.ps1。
.PARAMETER NotesFile
    Release 說明 Markdown 檔案路徑（選用）。
.PARAMETER UseExistingBundle
    使用已由本次工作流程建立並簽署證明的 bundle，不重新壓縮。
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Tag = "",
    [string]$Title = "",
    [string]$Configuration = "Release",
    [switch]$CreateRelease,
    [switch]$SkipValidation,
    [string]$NotesFile = "",
    [switch]$UseExistingBundle
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")
if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = "v$packageVersion"
}

if ([string]::IsNullOrWhiteSpace($Title)) {
    $Title = "OdfKit $packageVersion"
}

$outDir = Join-Path $repoRoot "artifacts/nuget"
$bundlePath = Join-Path $repoRoot "artifacts/OdfKit-nuget-packages.zip"
$sbomPath = Join-Path $repoRoot "artifacts/webfont-sbom/manifest.spdx.json"
$sidecarRoot = Join-Path $repoRoot "artifacts/webfont-sidecar"

Push-Location $repoRoot
try {
    if (-not $SkipValidation) {
        & (Join-Path $PSScriptRoot "Test-NuGetPack.ps1") -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $packages = @(
        Get-ChildItem -LiteralPath $outDir -Filter *.nupkg -File
        Get-ChildItem -LiteralPath $outDir -Filter *.snupkg -File
    )
    $hashManifest = Join-Path $outDir "SHA256SUMS"
    if ($packages.Count -eq 0) {
        throw "找不到套件：$outDir"
    }
    if (-not (Test-Path -LiteralPath $hashManifest -PathType Leaf)) {
        throw "找不到已驗證套件的 SHA-256 manifest：$hashManifest"
    }
    if (-not (Test-Path -LiteralPath $sbomPath -PathType Leaf)) {
        throw "找不到已驗證 WebFont SPDX SBOM：$sbomPath"
    }
    $sidecarAssets = @(
        Get-ChildItem -LiteralPath $sidecarRoot -Filter *.zip -File
        Get-Item -LiteralPath (Join-Path $sidecarRoot "OdfKit.WebFonts.Sidecar.Host-SHA256SUMS")
    )
    if ($sidecarAssets.Count -ne 3) {
        throw "NativeAOT WebFont sidecar 發布資產不完整：$sidecarRoot"
    }
    $bundleInputs = @($packages | ForEach-Object { $_.FullName }) +
        @($hashManifest, $sbomPath) +
        @($sidecarAssets | ForEach-Object { $_.FullName })

    $bundleDir = Split-Path -Parent $bundlePath
    if (-not (Test-Path -LiteralPath $bundleDir)) {
        New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null
    }

    if ($UseExistingBundle) {
        if (-not (Test-Path -LiteralPath $bundlePath -PathType Leaf)) {
            throw "找不到待發布且已證明的 bundle：$bundlePath"
        }
    }
    else {
        if (Test-Path -LiteralPath $bundlePath) {
            Remove-Item -LiteralPath $bundlePath -Force
        }

        Compress-Archive -Path $bundleInputs -DestinationPath $bundlePath -Force
    }

    $expectedBundleFiles = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::Ordinal)
    foreach ($inputPath in $bundleInputs) {
        $fileName = [IO.Path]::GetFileName($inputPath)
        if (-not $expectedBundleFiles.TryAdd($fileName, $inputPath)) {
            throw "Release bundle 輸入含重複檔名：$fileName"
        }
    }
    $zip = [IO.Compression.ZipFile]::OpenRead($bundlePath)
    try {
        $entries = @($zip.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
        if ($entries.Count -ne $expectedBundleFiles.Count) {
            throw "Release bundle 檔案數與待發布資產不一致。"
        }
        foreach ($entry in $entries) {
            $sourcePath = $null
            if (-not $expectedBundleFiles.TryGetValue($entry.FullName, [ref]$sourcePath)) {
                throw "Release bundle 含非預期資產：$($entry.FullName)"
            }
            $entryStream = $entry.Open()
            try {
                $entryHash = [Convert]::ToHexString(
                    [Security.Cryptography.SHA256]::HashData($entryStream))
            }
            finally {
                $entryStream.Dispose()
            }
            $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
            if (-not [string]::Equals($entryHash, $sourceHash, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Release bundle 資產與已驗證來源不一致：$($entry.FullName)"
            }
        }
    }
    finally {
        $zip.Dispose()
    }

    Write-Host ""
    Write-Host "GitHub Release 標籤：$Tag"
    Write-Host "標題：$Title"
    Write-Host "NuGet 資產（$($packages.Count) 個套件、SHA256SUMS、SPDX SBOM 與 1 個 zip 彙整）："
    foreach ($pkg in $packages) {
        Write-Host "  $($pkg.Name)"
    }

    Write-Host "  $(Split-Path -Leaf $bundlePath)"
    Write-Host "  $(Split-Path -Leaf $hashManifest)"
    Write-Host "  $(Split-Path -Leaf $sbomPath)"
    foreach ($asset in $sidecarAssets) {
        Write-Host "  $($asset.Name)"
    }

    if (-not $CreateRelease) {
        Write-Host ""
        Write-Host "乾跑完成。若要建立 GitHub Release（需 gh CLI 已登入）："
        Write-Host "  pwsh eng/Publish-GitHubRelease.ps1 -CreateRelease -Tag $Tag"
        return
    }

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if ($null -eq $gh) {
        throw "找不到 gh CLI。請安裝 GitHub CLI 並執行 gh auth login。"
    }

    $assetPaths = @($bundlePath, $hashManifest, $sbomPath) +
        @($sidecarAssets | ForEach-Object { $_.FullName }) +
        ($packages | ForEach-Object { $_.FullName })
    $ghArgs = @("release", "create", $Tag, "--title", $Title)
    if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
        $ghArgs += @("--notes-file", $NotesFile)
    }
    else {
        $ghArgs += @("--generate-notes")
    }

    $ghArgs += $assetPaths

    if ($PSCmdlet.ShouldProcess($Tag, "gh release create")) {
        Write-Host ""
        Write-Host "執行：gh $($ghArgs -join ' ')"
        & gh @ghArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host ""
    Write-Host "GitHub Release 建立完成。"
}
finally {
    Pop-Location
}
