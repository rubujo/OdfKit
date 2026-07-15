<#
.SYNOPSIS
建立真實 WOFF2 子集，並以最小 ASP.NET Core 應用程式驗證 HTTP 提供結果。
#>
[CmdletBinding()]
param(
    [string]$Destination = "artifacts/webfont-smoke",
    [string]$FontPath,
    [string]$MappingTablesRoot,
    [string]$PythonPath = "python",
    [switch]$SkipPythonInstall,
    [switch]$RunBrowser,
    [ValidateSet("chromium", "firefox", "webkit")]
    [string[]]$Browsers = @("chromium", "firefox", "webkit")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $PSScriptRoot "external-tools.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$configuration = $manifest.webFontSmoke

$destinationPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
$repoRootWithSeparator = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $destinationPath.StartsWith($repoRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination 必須位於方案目錄內。"
}

$assetPath = Join-Path $destinationPath "assets"
$productAssetPath = Join-Path $assetPath "product"
$productReproAssetPath = Join-Path $assetPath "product-repro"
$pythonVersionTag = (& $PythonPath -c "import sys; print(f'{sys.version_info.major}{sys.version_info.minor}')").Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($pythonVersionTag)) {
    throw "無法取得 Python 版本。"
}

$pythonModulePath = Join-Path $destinationPath (
    "python-ft$($configuration.pythonPackages.fontToolsVersion)-br$($configuration.pythonPackages.brotliVersion)-py$pythonVersionTag")
$sourcePath = Join-Path $destinationPath "sources"
New-Item -ItemType Directory -Path $assetPath -Force | Out-Null
New-Item -ItemType Directory -Path $sourcePath -Force | Out-Null

function Get-LockedFile {
    param(
        [Parameter(Mandatory)]$Definition,
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string]$Sha256
    )

    $path = Join-Path $sourcePath $FileName
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host "下載鎖定測試資產 $FileName..."
        Invoke-WebRequest -Uri $Definition.uri -OutFile $path
    }

    $actualSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $Sha256) {
        throw "$FileName 的 SHA-256 不符合鎖定值。"
    }

    return $path
}

function Get-ArchivedFont {
    param([Parameter(Mandatory)]$Definition)

    $archivePath = Get-LockedFile `
        -Definition $Definition `
        -FileName $Definition.archiveFileName `
        -Sha256 $Definition.archiveSha256
    $extractPath = Join-Path $sourcePath ([System.IO.Path]::GetFileNameWithoutExtension($Definition.archiveFileName))
    $font = Get-ChildItem -LiteralPath $extractPath -Filter $Definition.fileName -File -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $font) {
        New-Item -ItemType Directory -Path $extractPath -Force | Out-Null
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath -Force
        $font = Get-ChildItem -LiteralPath $extractPath -Filter $Definition.fileName -File -Recurse |
            Select-Object -First 1
    }

    if ($null -eq $font) {
        throw "$($Definition.archiveFileName) 不含 $($Definition.fileName)。"
    }

    $actualSha256 = (Get-FileHash -LiteralPath $font.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $Definition.sha256) {
        throw "$($Definition.fileName) 的 SHA-256 不符合鎖定值。"
    }

    return $font.FullName
}

if ([string]::IsNullOrWhiteSpace($FontPath)) {
    $FontPath = Join-Path $destinationPath $configuration.font.fileName
    if (-not (Test-Path -LiteralPath $FontPath)) {
        Write-Host "下載測試字型 $($configuration.font.version)..."
        Invoke-WebRequest -Uri $configuration.font.uri -OutFile $FontPath
    }
}

$resolvedFontPath = (Resolve-Path -LiteralPath $FontPath).Path
$actualFontSha256 = (Get-FileHash -LiteralPath $resolvedFontPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualFontSha256 -ne $configuration.font.sha256) {
    throw "測試字型 SHA-256 不符合鎖定值。"
}

if (-not $SkipPythonInstall -and -not (Test-Path -LiteralPath (Join-Path $pythonModulePath "fontTools"))) {
    New-Item -ItemType Directory -Path $pythonModulePath -Force | Out-Null
    $pipArguments = @(
        "-m",
        "pip",
        "install",
        "--disable-pip-version-check",
        "--target",
        $pythonModulePath,
        "fonttools==$($configuration.pythonPackages.fontToolsVersion)",
        "brotli==$($configuration.pythonPackages.brotliVersion)"
    )
    & $PythonPath @pipArguments
    if ($LASTEXITCODE -ne 0) {
        throw "安裝 FontTools 與 Brotli 失敗。"
    }
}

$previousProbePath = $env:PYTHONPATH
try {
    $env:PYTHONPATH = $pythonModulePath
    & $PythonPath -c "import brotli, fontTools; print(fontTools.__version__)" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "鎖定的 FontTools 或 Brotli 模組不可用；請移除 SkipPythonInstall 後重試。"
    }
}
finally {
    $env:PYTHONPATH = $previousProbePath
}

$previousPythonPath = $env:PYTHONPATH
try {
    if (Test-Path -LiteralPath $pythonModulePath) {
        $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
            $pythonModulePath
        }
        else {
            $pythonModulePath + [System.IO.Path]::PathSeparator + $previousPythonPath
        }
    }

    $subsetPath = Join-Path $assetPath "smoke.woff2"
    $metadataPath = Join-Path $assetPath "metadata.json"
    $subsetArguments = @(
        (Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/prepare_subset.py"),
        "--font",
        $resolvedFontPath,
        "--output",
        $subsetPath,
        "--metadata",
        $metadataPath
    )
    & $PythonPath @subsetArguments
    if ($LASTEXITCODE -ne 0) {
        throw "建立 WOFF2 子集失敗。"
    }

    $reproSubsetPath = Join-Path $assetPath "smoke-repro.woff2"
    $reproMetadataPath = Join-Path $assetPath "metadata-repro.json"
    try {
        $reproArguments = @(
            (Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/prepare_subset.py"),
            "--font",
            $resolvedFontPath,
            "--output",
            $reproSubsetPath,
            "--metadata",
            $reproMetadataPath
        )
        & $PythonPath @reproArguments
        if ($LASTEXITCODE -ne 0) {
            throw "重複建立 WOFF2 子集失敗。"
        }

        $subsetSha256 = (Get-FileHash -LiteralPath $subsetPath -Algorithm SHA256).Hash
        $reproSha256 = (Get-FileHash -LiteralPath $reproSubsetPath -Algorithm SHA256).Hash
        if ($subsetSha256 -ne $reproSha256) {
            throw "相同輸入未產生位元組相同的 WOFF2。"
        }
    }
    finally {
        Remove-Item -LiteralPath $reproSubsetPath, $reproMetadataPath -Force -ErrorAction SilentlyContinue
    }

    $international = $configuration.internationalFonts
    $arabicPath = Get-LockedFile -Definition $international.arabic -FileName $international.arabic.fileName -Sha256 $international.arabic.sha256
    $devanagariPath = Get-LockedFile -Definition $international.devanagari -FileName $international.devanagari.fileName -Sha256 $international.devanagari.sha256
    $cjkCollectionPath = Get-LockedFile -Definition $international.cjkCollection -FileName $international.cjkCollection.fileName -Sha256 $international.cjkCollection.sha256
    $cjkOpenTypePath = Get-LockedFile -Definition $international.cjkOpenType -FileName $international.cjkOpenType.fileName -Sha256 $international.cjkOpenType.sha256
    $ipamjPath = Get-ArchivedFont -Definition $international.ipamj
    $cnsSungPath = Get-ArchivedFont -Definition $international.cnsSung
    $internationalAssetPath = Join-Path $assetPath "international"
    $internationalArguments = @(
        (Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/prepare_international.py"),
        "--arabic", $arabicPath,
        "--devanagari", $devanagariPath,
        "--cjk-collection", $cjkCollectionPath,
        "--cjk-opentype", $cjkOpenTypePath,
        "--ipamj", $ipamjPath,
        "--cns-pua", $cnsSungPath,
        "--output", $internationalAssetPath
    )
    & $PythonPath @internationalArguments
    if ($LASTEXITCODE -ne 0) {
        throw "建立多國 WebFont 驗證資產失敗。"
    }

    Remove-Item -LiteralPath $productAssetPath, $productReproAssetPath -Recurse -Force -ErrorAction SilentlyContinue
    $buildProject = Join-Path $repoRoot "OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj"
    $contentRoot = Join-Path $repoRoot "tests/OdfKit.WebFontSmoke"
    foreach ($productOutput in @($productAssetPath, $productReproAssetPath)) {
        $buildArguments = @(
            "run",
            "--project", $buildProject,
            "-c", "Release",
            "--",
            "build",
            "--font", $resolvedFontPath,
            "--content-root", $contentRoot,
            "--content-extensions", ".txt,.cs",
            "--output", $productOutput,
            "--profile", "github-smoke-v1",
            "--family", "OdfKit Product Smoke",
            "--formats", "woff2,woff",
            "--pyftsubset", $PythonPath,
            "--fonttools-pythonpath", $pythonModulePath
        )
        dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "OdfKit.WebFonts.Build 自動內容掃描與產生失敗。"
        }
    }
}
finally {
    $env:PYTHONPATH = $previousPythonPath
}

$productManifest = Get-Content -LiteralPath (Join-Path $productAssetPath "webfonts.json") -Raw | ConvertFrom-Json
$productReproManifest = Get-Content -LiteralPath (Join-Path $productReproAssetPath "webfonts.json") -Raw | ConvertFrom-Json
if ($productManifest.profileId -ne "github-smoke-v1" -or @($productManifest.assets).Count -ne 2) {
    throw "產品化 Build smoke 的 manifest 不符合預期。"
}

foreach ($productAsset in $productManifest.assets) {
    $productFile = Join-Path $productAssetPath "$($productAsset.sha256)/$($productAsset.fileName)"
    if (-not (Test-Path -LiteralPath $productFile)) {
        throw "產品化 Build smoke 未建立 content-addressed 路徑：$productFile"
    }

    $actualProductHash = (Get-FileHash -LiteralPath $productFile -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualProductHash -ne $productAsset.sha256) {
        throw "產品化 Build smoke 的資產 hash 不正確。"
    }
}

$productHashes = @($productManifest.assets | Sort-Object format | ForEach-Object sha256) -join ","
$productReproHashes = @($productReproManifest.assets | Sort-Object format | ForEach-Object sha256) -join ","
if ($productHashes -ne $productReproHashes) {
    throw "OdfKit.WebFonts.Build 相同內容未產生可重現資產。"
}

$productCss = Get-Content -LiteralPath (Join-Path $productAssetPath "webfonts.css") -Raw
if ($productCss -notmatch "\./[a-f0-9]{64}/[^']+\.woff2") {
    throw "產品化 Build smoke 的 CSS 未使用 content-addressed URL。"
}
$fontFaceCount = ([regex]::Matches($productCss, "@font-face")).Count
$woff2Index = $productCss.IndexOf("format('woff2')", [StringComparison]::Ordinal)
$woffIndex = $productCss.IndexOf("format('woff')", [StringComparison]::Ordinal)
if ($fontFaceCount -ne 1 -or $woff2Index -lt 0 -or $woffIndex -le $woff2Index) {
    throw "產品化 Build smoke 的 CSS 未將 WOFF2／WOFF 合併為 WOFF2 優先的 src fallback。"
}

if (-not [string]::IsNullOrWhiteSpace($MappingTablesRoot)) {
    $unicodeDirectory = Join-Path $MappingTablesRoot "Unicode"
    $expectedMappings = [ordered]@{
        "3-216F" = "201A9"
        "4-2121" = "20086"
        "5-2121" = "200D1"
        "6-2135" = "201A4"
        "7-2155" = "20F64"
        "10-2143" = "2003E"
        "11-2121" = "270AE"
        "12-5250" = "205EB"
        "15-212D" = "20630"
    }
    $mappingFiles = Get-ChildItem -LiteralPath $unicodeDirectory -File
    foreach ($mapping in $expectedMappings.GetEnumerator()) {
        $mappingMatch = $mappingFiles |
            Select-String -Pattern "^$([regex]::Escape($mapping.Key))\t$($mapping.Value)$" |
            Select-Object -First 1
        if ($null -eq $mappingMatch) {
            throw "CNS 11643 官方對照表找不到 $($mapping.Key) → U+$($mapping.Value)。"
        }
    }

    Write-Host "CNS 11643 對照：已驗證第 3、4、5、6、7、10、11、12、15 字面。"
}

$projectPath = Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/OdfKit.WebFontSmoke.csproj"
$smokeIntermediateRoot = Join-Path $destinationPath "smoke-project-obj"
dotnet restore $projectPath -p:NuGetAudit=false -p:OdfKitWebFontSmokeIntermediateRoot="$smokeIntermediateRoot\"
if ($LASTEXITCODE -ne 0) {
    throw "WebFont smoke 專案還原失敗。"
}

dotnet build $projectPath -c Release --no-restore -p:NuGetAudit=false -p:OdfKitWebFontSmokeIntermediateRoot="$smokeIntermediateRoot\"
if ($LASTEXITCODE -ne 0) {
    throw "WebFont smoke 專案建置失敗。"
}

$listener = [System.Net.Sockets.TcpListener]::new(
    [System.Net.IPAddress]::Loopback,
    0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$url = "http://127.0.0.1:$port"
$appDll = Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/bin/Release/net10.0/OdfKit.WebFontSmoke.dll"
$env:ODFKIT_WEBFONT_SMOKE_ASSETS = $assetPath
$startParameters = @{
    FilePath = "dotnet"
    ArgumentList = @("""$appDll""", "--urls", $url)
    WindowStyle = "Hidden"
    PassThru = $true
}
$process = Start-Process @startParameters

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($process.HasExited) {
            throw "WebFont smoke 伺服器提前結束。"
        }

        try {
            $health = Invoke-RestMethod -Uri "$url/health"
            break
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    if ($null -eq $health -or $health.status -ne "ok" -or $health.signature -ne "wOF2") {
        throw "WebFont smoke 健康檢查失敗。"
    }

    $unicodePlanes = @(
        $health.testCases |
            ForEach-Object { $_.unicodePlane } |
            Sort-Object -Unique
    ) -join ","
    if (@($health.testCases).Count -ne 13 -or $unicodePlanes -ne "0,1,2,3") {
        throw "WebFont smoke 未涵蓋預期的 13 個字元與 Unicode Plane 0～3。"
    }

    $internationalHealth = Invoke-RestMethod -Uri "$url/international/health"
    if ($internationalHealth.status -ne "ok" -or $internationalHealth.caseCount -ne 6 -or $internationalHealth.assetCount -ne 11) {
        throw "多國 WebFont smoke 未涵蓋預期的六個案例與十一個資產。"
    }

    $internationalManifest = Get-Content -LiteralPath (Join-Path $assetPath "international/webfonts.json") -Raw | ConvertFrom-Json
    foreach ($internationalAsset in $internationalManifest.assets) {
        $hostedAsset = Invoke-WebRequest -Uri "$url/_odf-fonts/$($internationalAsset.sha256)/$($internationalAsset.fileName)"
        if ([int64]$hostedAsset.RawContentLength -ne [int64]$internationalAsset.byteLength) {
            throw "託管資產 $($internationalAsset.fileName) 長度不正確。"
        }

        if ($hostedAsset.Headers["Cache-Control"] -notmatch "immutable") {
            throw "託管資產 $($internationalAsset.fileName) 未使用 immutable cache。"
        }
    }

    $page = Invoke-WebRequest -Uri $url
    $font = Invoke-WebRequest -Uri "$url/font.woff2"
    if ($page.Content -notmatch "OdfKit WebFont 最小驗證") {
        throw "展示頁內容不符合預期。"
    }

    if ($font.Headers["Content-Type"] -notmatch "^font/woff2") {
        throw "WOFF2 的 Content-Type 不正確。"
    }

    Write-Host "PASS：$url"
    Write-Host "來源：$($health.sourceBytes) bytes；WOFF2：$($health.subsetBytes) bytes"
    Write-Host "Unicode Plane：$unicodePlanes"
    Write-Host "字元：$($health.codePoints)"
    Write-Host "多國案例：$($internationalHealth.caseCount)；多格式資產：$($internationalHealth.assetCount)"

    if ($RunBrowser) {
        $browserProject = Join-Path $repoRoot "tests/OdfKit.WebFontBrowserSmoke/OdfKit.WebFontBrowserSmoke.csproj"
        dotnet build $browserProject -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Playwright WebFont browser smoke 建置失敗。"
        }

        $playwrightInstaller = Join-Path $repoRoot "tests/OdfKit.WebFontBrowserSmoke/bin/Release/net10.0/playwright.ps1"
        & $playwrightInstaller install @Browsers
        if ($LASTEXITCODE -ne 0) {
            throw "Playwright 瀏覽器安裝失敗。"
        }

        foreach ($browser in $Browsers) {
            $browserScreenshot = Join-Path $destinationPath "playwright-international-$browser.png"
            dotnet run --project $browserProject -c Release --no-build -- `
                "$url/international" $browser $browserScreenshot
            if ($LASTEXITCODE -ne 0) {
                throw "Playwright WebFont $browser smoke 失敗。"
            }
        }
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }

    Remove-Item Env:ODFKIT_WEBFONT_SMOKE_ASSETS -ErrorAction SilentlyContinue
}
