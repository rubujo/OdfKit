#Requires -Version 7.0
<#
.SYNOPSIS
以三瀏覽器比較真實 CFF／CFF2、阿拉伯文與 Devanagari variable 來源及 managed subset 的像素。
#>
[CmdletBinding()]
param(
    [string]$FormatMatrixRoot = "artifacts/webfont-format-matrix",
    [string]$Destination = "artifacts/webfont-layout-browser",
    [ValidateSet("chromium", "firefox", "webkit")]
    [string[]]$Browsers = @("chromium", "firefox", "webkit"),
    [ValidateRange(30, 600)]
    [int]$BrowserTimeoutSeconds = 120,
    [switch]$InstallBrowsers
)

$ErrorActionPreference = "Stop"

function Invoke-LayoutBrowserSmoke {
    param(
        [Parameter(Mandatory)][string]$AppDll,
        [Parameter(Mandatory)][string]$BrowserName,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add($AppDll)
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "$BrowserName 複雜塑形差分驗證程序無法啟動。"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            if (-not $process.HasExited) {
                $process.Kill($true)
            }
            $process.WaitForExit()
        }
        else {
            $process.WaitForExit()
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult().Trim()
        $stderr = $stderrTask.GetAwaiter().GetResult().Trim()
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            Write-Host $stdout
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Write-Warning "$BrowserName stderr：`n$stderr"
        }
        if ($timedOut) {
            throw "$BrowserName 複雜塑形差分驗證逾時（$TimeoutSeconds 秒）。"
        }
        if ($process.ExitCode -ne 0) {
            throw "$BrowserName 複雜塑形差分驗證失敗，結束碼為 $($process.ExitCode)。"
        }
    }
    finally {
        $process.Dispose()
    }
}

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
$cffSource = Join-Path $sourceRoot "SourceHanSansTC-Regular.otf"
$cffSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff-otf/first") `
        -Filter "*.woff2" -File -Recurse)
$arabicVariableSource = Join-Path $sourceRoot "NotoSansArabic-VF.ttf"
$devanagariVariableSource = Join-Path $sourceRoot "NotoSansDevanagari-VF.ttf"
$arabicVariableSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "arabic-variable/first") `
        -Filter "*.woff2" -File -Recurse)
$devanagariVariableSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "devanagari-variable/first") `
        -Filter "*.woff2" -File -Recurse)
$cff2VariableSource = Join-Path $sourceRoot "SourceHanSansTW-VF.otf"
$cff2VariableSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff2-variable/first") `
        -Filter "*.woff2" -File -Recurse)
$cffCollectionSource = Join-Path $sourceRoot "NotoSansCJK-Regular.ttc"
$cffCollectionSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff-otc-face-0/first") `
        -Filter "*.woff2" -File -Recurse)
$cffCollectionStandalone = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff-otc-face-0/first") `
        -Filter "*.otf" -File -Recurse)
$cff2CollectionSource = Join-Path $evidenceRoot "source-han-cff2-variable.otc"
$cff2CollectionSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff2-otc-variable/first") `
        -Filter "*.woff2" -File -Recurse)
$cff2CollectionStandalone = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff2-otc-variable/first") `
        -Filter "*.otf" -File -Recurse)
$colorColrV1Source = Join-Path $sourceRoot "Noto-COLRv1.ttf"
$colorColrV1Subsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "color-colrv1/first") `
        -Filter "*.woff2" -File -Recurse)
if (-not (Test-Path -LiteralPath $arabicSource) `
    -or -not (Test-Path -LiteralPath $devanagariSource) `
    -or -not (Test-Path -LiteralPath $cffSource) `
    -or -not (Test-Path -LiteralPath $arabicVariableSource) `
    -or -not (Test-Path -LiteralPath $devanagariVariableSource) `
    -or -not (Test-Path -LiteralPath $cff2VariableSource) `
    -or -not (Test-Path -LiteralPath $cffCollectionSource) `
    -or -not (Test-Path -LiteralPath $cff2CollectionSource) `
    -or -not (Test-Path -LiteralPath $colorColrV1Source) `
    -or $arabicSubsets.Count -ne 1 `
    -or $devanagariSubsets.Count -ne 1 `
    -or $cffSubsets.Count -ne 1 `
    -or $arabicVariableSubsets.Count -ne 1 `
    -or $devanagariVariableSubsets.Count -ne 1 `
    -or $cff2VariableSubsets.Count -ne 1 `
    -or $cffCollectionSubsets.Count -ne 1 `
    -or $cffCollectionStandalone.Count -ne 1 `
    -or $cff2CollectionSubsets.Count -ne 1 `
    -or $cff2CollectionStandalone.Count -ne 1 `
    -or $colorColrV1Subsets.Count -ne 1) {
    throw "請先執行 eng/Test-WebFontFormatMatrix.ps1 產生 layout corpus。"
}

$project = Join-Path $repoRoot "tests/OdfKit.WebFontBrowserSmoke/OdfKit.WebFontBrowserSmoke.csproj"
dotnet build $project -c Release --nologo --no-restore
if ($LASTEXITCODE -ne 0) { throw "WebFont browser smoke 建置失敗。" }
$appDll = Join-Path $repoRoot `
    "tests/OdfKit.WebFontBrowserSmoke/bin/Release/net10.0/OdfKit.WebFontBrowserSmoke.dll"
if (-not (Test-Path -LiteralPath $appDll -PathType Leaf)) {
    throw "WebFont browser smoke 程式不存在。"
}
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
    $cffCollectionBrowserSource = if ($browser -eq "chromium") {
        $cffCollectionSource
    }
    else {
        $cffCollectionStandalone[0].FullName
    }
    $cff2CollectionBrowserSource = if ($browser -eq "chromium") {
        $cff2CollectionSource
    }
    else {
        $cff2CollectionStandalone[0].FullName
    }
    $screenshot = Join-Path $destinationRoot "layout-$browser.png"
    $evidence = Join-Path $destinationRoot "layout-$browser.json"
    Write-Host "驗證 $browser 複雜塑形差分（上限 $BrowserTimeoutSeconds 秒）…"
    Invoke-LayoutBrowserSmoke `
        -AppDll $appDll `
        -BrowserName $browser `
        -TimeoutSeconds $BrowserTimeoutSeconds `
        -Arguments @(
            "layout",
            $browser,
            $arabicSource,
            $arabicSubsets[0].FullName,
            $devanagariSource,
            $devanagariSubsets[0].FullName,
            $cffSource,
            $cffSubsets[0].FullName,
            $arabicVariableSource,
            $arabicVariableSubsets[0].FullName,
            $devanagariVariableSource,
            $devanagariVariableSubsets[0].FullName,
            $cff2VariableSource,
            $cff2VariableSubsets[0].FullName,
            $cffCollectionBrowserSource,
            $cffCollectionSubsets[0].FullName,
            $cff2CollectionBrowserSource,
            $cff2CollectionSubsets[0].FullName,
            $colorColrV1Source,
            $colorColrV1Subsets[0].FullName,
            $screenshot,
            $evidence)
}

Write-Host "PASS：CFF／CFF2 OTC 與 COLRv1 color 輸入均轉為獨立 WOFF2，且輸出在三瀏覽器像素一致；原始 OTC 僅另由 Chromium 作能力佐證。"
