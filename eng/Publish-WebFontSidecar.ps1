#Requires -Version 7.0
<#
.SYNOPSIS
    建立 Windows NativeAOT WebFont sidecar 的可發布 ZIP 與 SHA-256 manifest。
.PARAMETER Configuration
    建置組態，預設為 Release。
.PARAMETER OutputDirectory
    相對於 repository 的輸出目錄。
.PARAMETER SkipPublish
    使用已存在的 AOT 產物；只供本機驗證封裝階段。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/webfont-sidecar",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts")) +
    [IO.Path]::DirectorySeparatorChar
$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")
$rids = @("win-x64", "win-arm64")

if (-not ($outputRoot + [IO.Path]::DirectorySeparatorChar).StartsWith(
        $artifactsRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Sidecar 輸出目錄必須位於 repository artifacts 內。"
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

foreach ($rid in $rids) {
    if (-not $SkipPublish) {
        & (Join-Path $PSScriptRoot "Test-WebFontSidecarAot.ps1") `
            -Configuration $Configuration `
            -RuntimeIdentifier $rid `
            -PublishOnly
        if ($LASTEXITCODE -ne 0) {
            throw "WebFont sidecar $rid 發布失敗，結束碼 $LASTEXITCODE。"
        }
    }

    $publishedExecutable = Join-Path $repoRoot (
        "artifacts/webfont-sidecar-aot-$rid/OdfKit.WebFonts.Sidecar.Host.exe")
    if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
        throw "WebFont sidecar $rid 缺少原生執行檔。"
    }

    $staging = Join-Path $outputRoot $rid
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item -LiteralPath $publishedExecutable -Destination $staging
    Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $staging
    Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") -Destination $staging
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs/nativeaot.md") -Destination $staging
    Set-Content -LiteralPath (Join-Path $staging "VERSION.txt") -Value $packageVersion -Encoding utf8NoBOM

    $archive = Join-Path $outputRoot "OdfKit.WebFonts.Sidecar.Host-$packageVersion-$rid.zip"
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $archive -CompressionLevel Optimal
    Remove-Item -LiteralPath $staging -Recurse -Force
}

$manifestLines = Get-ChildItem -LiteralPath $outputRoot -Filter "*.zip" -File |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
$manifestLines | Set-Content `
    -LiteralPath (Join-Path $outputRoot "OdfKit.WebFonts.Sidecar.Host-SHA256SUMS") `
    -Encoding utf8NoBOM
Write-Host "NativeAOT WebFont sidecar 發布資產已建立：$outputRoot"
