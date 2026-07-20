#Requires -Version 7.0
<#
.SYNOPSIS
以三瀏覽器比較真實 layout、variable 與瀏覽器可用 color 來源及 managed subset 的像素。
#>
[CmdletBinding()]
param(
    [string]$FormatMatrixRoot = "artifacts/webfont-format-matrix",
    [string]$Destination = "artifacts/webfont-layout-browser",
    [ValidateSet("chromium", "firefox", "webkit")]
    [string[]]$Browsers = @("chromium", "firefox", "webkit"),
    [ValidateRange(30, 600)]
    [int]$BrowserTimeoutSeconds = 120,
    [string]$ChromiumExecutablePath,
    [switch]$InstallBrowsers
)

$ErrorActionPreference = "Stop"

function Invoke-LayoutBrowserSmoke {
    param(
        [Parameter(Mandatory)][string]$AppDll,
        [Parameter(Mandatory)][string]$BrowserName,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [string]$ChromiumExecutablePath
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    if ($BrowserName -eq "chromium" -and -not [string]::IsNullOrWhiteSpace($ChromiumExecutablePath)) {
        $startInfo.Environment["ODFKIT_PLAYWRIGHT_CHROMIUM_EXECUTABLE"] = $ChromiumExecutablePath
    }
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
        if ($IsWindows -and $BrowserName -eq 'firefox') {
            $browserRoot = if ([string]::IsNullOrWhiteSpace($env:PLAYWRIGHT_BROWSERS_PATH)) {
                Join-Path $env:LOCALAPPDATA 'ms-playwright'
            }
            else {
                [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) $env:PLAYWRIGHT_BROWSERS_PATH))
            }
            & (Join-Path $PSScriptRoot 'Remove-PlaywrightFirefoxPrivateBrowsingShortcut.ps1') `
                -BrowserRoot $browserRoot | Out-Null
        }
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$resolvedChromium = $null
if (-not [string]::IsNullOrWhiteSpace($ChromiumExecutablePath)) {
    $resolvedChromium = [IO.Path]::GetFullPath($ChromiumExecutablePath)
    if (-not (Test-Path -LiteralPath $resolvedChromium -PathType Leaf)) {
        throw "ChromiumExecutablePath 不存在或不是檔案。"
    }
}
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
$nameCffSources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter "SourceCodePro-Regular.otf" `
        -File -Recurse)
$nameCffSource = if ($nameCffSources.Count -eq 1) { $nameCffSources[0].FullName } else { $null }
$nameCffSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff-name-otf/first") `
        -Filter "*.woff2" -File -Recurse)
$seacCffSource = Join-Path $sourceRoot "afdko-seac.otf"
$seacCffSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff-name-seac/first") `
        -Filter "*.woff2" -File -Recurse)
$staticCff2Source = Join-Path $sourceRoot "afdko-regular-CFF2.otf"
$staticCff2Subsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "cff2-static/first") `
        -Filter "*.woff2" -File -Recurse)
$arabicVariableSource = Join-Path $sourceRoot "NotoSansArabic-VF.ttf"
$devanagariVariableSource = Join-Path $sourceRoot "NotoSansDevanagari-VF.ttf"
$arabicVariableSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "arabic-variable/first") `
        -Filter "*.woff2" -File -Recurse)
$devanagariVariableSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "devanagari-variable/first") `
        -Filter "*.woff2" -File -Recurse)
$bengaliSource = Join-Path $sourceRoot "NotoSansBengali-VF.ttf"
$bengaliSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "bengali-variable-layout/first") `
        -Filter "*.woff2" -File -Recurse)
$khmerSource = Join-Path $sourceRoot "NotoSansKhmer-VF.ttf"
$khmerSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "khmer-variable-layout/first") `
        -Filter "*.woff2" -File -Recurse)
$thaiSource = Join-Path $sourceRoot "NotoSansThai-VF.ttf"
$thaiSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "thai-variable-layout/first") `
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
$colorSbixSource = Join-Path $sourceRoot "samples-sbix.ttf"
$colorSbixSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "color-sbix/first") `
        -Filter "*.woff2" -File -Recurse)
$colorSvgSource = Join-Path $sourceRoot "samples-picosvg.ttf"
$colorSvgSubsets = @(Get-ChildItem -LiteralPath (Join-Path $evidenceRoot "color-svg/first") `
        -Filter "*.woff2" -File -Recurse)
if (-not (Test-Path -LiteralPath $arabicSource) `
    -or -not (Test-Path -LiteralPath $devanagariSource) `
    -or -not (Test-Path -LiteralPath $cffSource) `
    -or $null -eq $nameCffSource `
    -or -not (Test-Path -LiteralPath $seacCffSource) `
    -or -not (Test-Path -LiteralPath $staticCff2Source) `
    -or -not (Test-Path -LiteralPath $arabicVariableSource) `
    -or -not (Test-Path -LiteralPath $devanagariVariableSource) `
    -or -not (Test-Path -LiteralPath $bengaliSource) `
    -or -not (Test-Path -LiteralPath $khmerSource) `
    -or -not (Test-Path -LiteralPath $thaiSource) `
    -or -not (Test-Path -LiteralPath $cff2VariableSource) `
    -or -not (Test-Path -LiteralPath $cffCollectionSource) `
    -or -not (Test-Path -LiteralPath $cff2CollectionSource) `
    -or -not (Test-Path -LiteralPath $colorColrV1Source) `
    -or -not (Test-Path -LiteralPath $colorSbixSource) `
    -or -not (Test-Path -LiteralPath $colorSvgSource) `
    -or $arabicSubsets.Count -ne 1 `
    -or $devanagariSubsets.Count -ne 1 `
    -or $cffSubsets.Count -ne 1 `
    -or $nameCffSubsets.Count -ne 1 `
    -or $seacCffSubsets.Count -ne 1 `
    -or $staticCff2Subsets.Count -ne 1 `
    -or $arabicVariableSubsets.Count -ne 1 `
    -or $devanagariVariableSubsets.Count -ne 1 `
    -or $bengaliSubsets.Count -ne 1 `
    -or $khmerSubsets.Count -ne 1 `
    -or $thaiSubsets.Count -ne 1 `
    -or $cff2VariableSubsets.Count -ne 1 `
    -or $cffCollectionSubsets.Count -ne 1 `
    -or $cffCollectionStandalone.Count -ne 1 `
    -or $cff2CollectionSubsets.Count -ne 1 `
    -or $cff2CollectionStandalone.Count -ne 1 `
    -or $colorColrV1Subsets.Count -ne 1 `
    -or $colorSbixSubsets.Count -ne 1 `
    -or $colorSvgSubsets.Count -ne 1) {
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

if ($IsWindows -and $Browsers -contains 'firefox') {
    $browserRoot = if ([string]::IsNullOrWhiteSpace($env:PLAYWRIGHT_BROWSERS_PATH)) {
        Join-Path $env:LOCALAPPDATA 'ms-playwright'
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $repoRoot $env:PLAYWRIGHT_BROWSERS_PATH))
    }
    & (Join-Path $PSScriptRoot 'Remove-PlaywrightFirefoxPrivateBrowsingShortcut.ps1') `
        -BrowserRoot $browserRoot | Out-Null
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
        -ChromiumExecutablePath $resolvedChromium `
        -Arguments @(
            "layout",
            $browser,
            "arabic-source=$($arabicSource)",
            "arabic-subset=$($arabicSubsets[0].FullName)",
            "devanagari-source=$($devanagariSource)",
            "devanagari-subset=$($devanagariSubsets[0].FullName)",
            "cff-source=$($cffSource)",
            "cff-subset=$($cffSubsets[0].FullName)",
            "name-cff-source=$($nameCffSource)",
            "name-cff-subset=$($nameCffSubsets[0].FullName)",
            "seac-cff-source=$($seacCffSource)",
            "seac-cff-subset=$($seacCffSubsets[0].FullName)",
            "static-cff2-source=$($staticCff2Source)",
            "static-cff2-subset=$($staticCff2Subsets[0].FullName)",
            "arabic-variable-source=$($arabicVariableSource)",
            "arabic-variable-subset=$($arabicVariableSubsets[0].FullName)",
            "devanagari-variable-source=$($devanagariVariableSource)",
            "devanagari-variable-subset=$($devanagariVariableSubsets[0].FullName)",
            "bengali-source=$($bengaliSource)",
            "bengali-subset=$($bengaliSubsets[0].FullName)",
            "khmer-source=$($khmerSource)",
            "khmer-subset=$($khmerSubsets[0].FullName)",
            "thai-source=$($thaiSource)",
            "thai-subset=$($thaiSubsets[0].FullName)",
            "cff2-variable-source=$($cff2VariableSource)",
            "cff2-variable-subset=$($cff2VariableSubsets[0].FullName)",
            "cff-collection-source=$($cffCollectionBrowserSource)",
            "cff-collection-subset=$($cffCollectionSubsets[0].FullName)",
            "cff2-collection-source=$($cff2CollectionBrowserSource)",
            "cff2-collection-subset=$($cff2CollectionSubsets[0].FullName)",
            "color-colrv1-source=$($colorColrV1Source)",
            "color-colrv1-subset=$($colorColrV1Subsets[0].FullName)",
            "color-sbix-source=$($colorSbixSource)",
            "color-sbix-subset=$($colorSbixSubsets[0].FullName)",
            "color-svg-source=$($colorSvgSource)",
            "color-svg-subset=$($colorSvgSubsets[0].FullName)",
            "screenshot=$($screenshot)",
            "evidence=$($evidence)")
}

$browserSummary = $Browsers -join "／"
$rawCollectionSummary = if ($Browsers -contains "chromium") {
    "本次另由 Chromium 驗證原始 OTC"
}
else {
    "本次未驗證原始 OTC"
}
Write-Host "PASS：Arabic／Devanagari／Bengali／Khmer／Thai、CID／名稱式 CFF、真實 seac、靜態 CFF2、CFF2 OTC 與各瀏覽器明確可用的 color 模型均轉為獨立 WOFF2，且輸出在 $browserSummary 像素一致；不支援的 color 模型記錄為 browser-unavailable；$rawCollectionSummary。"
