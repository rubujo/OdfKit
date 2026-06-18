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
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Tag = "",
    [string]$Title = "",
    [string]$Configuration = "Release",
    [switch]$CreateRelease,
    [switch]$SkipValidation,
    [string]$NotesFile = ""
)

$ErrorActionPreference = "Stop"

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
    if ($packages.Count -eq 0) {
        throw "找不到套件：$outDir"
    }

    $bundleDir = Split-Path -Parent $bundlePath
    if (-not (Test-Path -LiteralPath $bundleDir)) {
        New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null
    }

    if (Test-Path -LiteralPath $bundlePath) {
        Remove-Item -LiteralPath $bundlePath -Force
    }

    Compress-Archive -Path ($packages | ForEach-Object { $_.FullName }) -DestinationPath $bundlePath -Force

    Write-Host ""
    Write-Host "GitHub Release 標籤：$Tag"
    Write-Host "標題：$Title"
    Write-Host "NuGet 資產（$($packages.Count) 個檔案 + 1 個 zip 彙整）："
    foreach ($pkg in $packages) {
        Write-Host "  $($pkg.Name)"
    }

    Write-Host "  $(Split-Path -Leaf $bundlePath)"

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

    $assetPaths = @($bundlePath) + ($packages | ForEach-Object { $_.FullName })
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