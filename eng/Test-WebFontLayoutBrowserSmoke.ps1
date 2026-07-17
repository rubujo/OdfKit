#Requires -Version 7.0
<#
.SYNOPSIS
以三瀏覽器比較真實阿拉伯文與 Devanagari 來源字型及 managed subset 的塑形像素。
#>
[CmdletBinding()]
param(
    [string]$FormatMatrixRoot = "artifacts/webfont-format-matrix",
    [string]$Destination = "artifacts/webfont-layout-browser",
    [ValidateSet("chromium", "firefox", "webkit")]
    [string[]]$Browsers = @("chromium", "firefox", "webkit"),
    [switch]$InstallBrowsers
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$matrixRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $FormatMatrixRoot))
$destinationRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
foreach ($path in @($matrixRoot, $destinationRoot)) {
    if (-not $path.StartsWith($repoPrefix, $comparison)) {
        throw "測試路徑必須位於方案目錄內。"
    }
}

$sourceRoot = Join-Path $matrixRoot "sources"
$evidenceRoot = Join-Path $matrixRoot "evidence"
$arabicSource = Join-Path $sourceRoot "NotoSansArabic-Regular.ttf"
$devanagariSource = Join-Path $sourceRoot "NotoSansDevanagari-Regular.ttf"
$arabicSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "arabic-static-layout/first") `
        -Filter "*.woff2" -File -Recurse)
$devanagariSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "devanagari-static-layout/first") `
        -Filter "*.woff2" -File -Recurse)
if (-not (Test-Path -LiteralPath $arabicSource) `
    -or -not (Test-Path -LiteralPath $devanagariSource) `
    -or $arabicSubsets.Count -ne 1 `
    -or $devanagariSubsets.Count -ne 1) {
    throw "請先執行 eng/Test-WebFontFormatMatrix.ps1 產生 layout corpus。"
}

$project = Join-Path $repoRoot "tests/OdfKit.WebFontBrowserSmoke/OdfKit.WebFontBrowserSmoke.csproj"
dotnet build $project -c Release --nologo --no-restore
if ($LASTEXITCODE -ne 0) { throw "WebFont browser smoke 建置失敗。" }
if ($InstallBrowsers) {
    $installer = Join-Path $repoRoot "tests/OdfKit.WebFontBrowserSmoke/bin/Release/net10.0/playwright.ps1"
    & $installer install @Browsers
    if ($LASTEXITCODE -ne 0) { throw "Playwright 瀏覽器安裝失敗。" }
    if ($IsWindows) {
        $browserRoot = if ([string]::IsNullOrWhiteSpace($env:PLAYWRIGHT_BROWSERS_PATH)) {
            Join-Path $env:LOCALAPPDATA 'ms-playwright'
        }
        else {
            [IO.Path]::GetFullPath((Join-Path $repoRoot $env:PLAYWRIGHT_BROWSERS_PATH))
        }
        & (Join-Path $PSScriptRoot 'Remove-PlaywrightFirefoxPrivateBrowsingShortcut.ps1') `
            -BrowserRoot $browserRoot | Out-Null
    }
}

New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
foreach ($browser in $Browsers) {
    $screenshot = Join-Path $destinationRoot "layout-$browser.png"
    $evidence = Join-Path $destinationRoot "layout-$browser.json"
    dotnet run --project $project -c Release --no-build -- `
        layout `
        $browser `
        $arabicSource `
        $arabicSubsets[0].FullName `
        $devanagariSource `
        $devanagariSubsets[0].FullName `
        $screenshot `
        $evidence
    if ($LASTEXITCODE -ne 0) { throw "$browser 複雜塑形差分驗證失敗。" }
}

Write-Host "PASS：阿拉伯文與 Devanagari 來源／subset 在 $($Browsers -join '／') 的塑形像素一致。"
