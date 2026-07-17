#Requires -Version 7.0
<#
.SYNOPSIS
以真實 CNS 11643 字型驗證純 .NET 子集、HTTP 動態產字與三瀏覽器載入。
#>
[CmdletBinding()]
param(
    [string]$Destination = "artifacts/webfont-smoke",
    [string]$FontPath,
    [string]$CnsFontArchivePath,
    [string]$MappingTablesRoot,
    [switch]$RunBrowser,
    [ValidateSet("chromium", "firefox", "webkit")]
    [string[]]$Browsers = @("chromium", "firefox", "webkit")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "external-tools.json") -Raw | ConvertFrom-Json
$fontDefinition = $manifest.webFontSmoke.internationalFonts.cnsExtB
$destinationPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
$repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $destinationPath.StartsWith($repoPrefix, $comparison)) {
    throw "Destination 必須位於方案目錄內。"
}

$sourceDirectory = Join-Path $destinationPath "sources"
$assetPath = Join-Path $destinationPath "assets"
$reproPath = Join-Path $destinationPath "assets-repro"
New-Item -ItemType Directory -Path $sourceDirectory -Force | Out-Null

function Invoke-LockedDownload {
    param([Parameter(Mandatory)][uri]$Uri, [Parameter(Mandatory)][string]$Destination)

    $temporaryPath = "$Destination.download"
    try {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        $downloaded = $false
        for ($attempt = 1; $attempt -le 4; $attempt++) {
            try {
                Invoke-WebRequest -Uri $Uri -OutFile $temporaryPath `
                    -MaximumRetryCount 3 -RetryIntervalSec 2 -TimeoutSec 180
                $downloaded = $true
                break
            }
            catch {
                Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
                if ($attempt -eq 4) { throw }
                Write-Warning "下載失敗，將重試鎖定來源（第 $attempt 次）：$Uri"
                Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
            }
        }
        if (-not $downloaded) { throw "無法下載鎖定的 WebFont 測試來源：$Uri" }
        Move-Item -LiteralPath $temporaryPath -Destination $Destination -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

if ([string]::IsNullOrWhiteSpace($FontPath)) {
    $archivePath = if ([string]::IsNullOrWhiteSpace($CnsFontArchivePath)) {
        Join-Path $sourceDirectory $fontDefinition.archiveFileName
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $repoRoot $CnsFontArchivePath))
    }
    if (-not $archivePath.StartsWith($repoPrefix, $comparison)) {
        throw "CnsFontArchivePath 必須位於方案目錄內。"
    }
    if (-not (Test-Path -LiteralPath $archivePath)) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $archivePath) -Force | Out-Null
        Invoke-LockedDownload -Uri $fontDefinition.uri -Destination $archivePath
    }
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($archiveHash -ne $fontDefinition.archiveSha256) {
        throw "CNS 11643 字型封存檔 SHA-256 不符合鎖定值。"
    }

    $extractPath = Join-Path $sourceDirectory "cns-sung"
    $font = Get-ChildItem -LiteralPath $extractPath -Filter $fontDefinition.fileName -File -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $font) {
        New-Item -ItemType Directory -Path $extractPath -Force | Out-Null
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath -Force
        $font = Get-ChildItem -LiteralPath $extractPath -Filter $fontDefinition.fileName -File -Recurse |
            Select-Object -First 1
    }
    if ($null -eq $font) {
        throw "CNS 11643 字型封存檔不含鎖定的 TTF。"
    }
    $FontPath = $font.FullName
}

$resolvedFontPath = (Resolve-Path -LiteralPath $FontPath).Path
$sourceSha256 = (Get-FileHash -LiteralPath $resolvedFontPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceSha256 -ne $fontDefinition.sha256) {
    throw "CNS 11643 字型 SHA-256 不符合鎖定值。"
}

$buildProject = Join-Path $repoRoot "OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj"
$contentRoot = Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/product-content"
foreach ($output in @($assetPath, $reproPath)) {
    Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue
    dotnet run --project $buildProject -c Release -- `
        build `
        --font $resolvedFontPath `
        --content-root $contentRoot `
        --content-extensions .txt `
        --output $output `
        --profile cns11643-managed-smoke-v1 `
        --family "OdfKit Product Smoke" `
        --formats woff2,woff,ttf
    if ($LASTEXITCODE -ne 0) {
        throw "純 .NET WebFont Build smoke 失敗。"
    }
}

$productManifest = Get-Content -LiteralPath (Join-Path $assetPath "webfonts.json") -Raw | ConvertFrom-Json
$reproManifest = Get-Content -LiteralPath (Join-Path $reproPath "webfonts.json") -Raw | ConvertFrom-Json
if ($productManifest.profileId -ne "cns11643-managed-smoke-v1" -or @($productManifest.assets).Count -ne 3) {
    throw "純 .NET Build manifest 不符合預期。"
}

foreach ($asset in $productManifest.assets) {
    $path = Join-Path $assetPath "$($asset.sha256)/$($asset.fileName)"
    if (-not (Test-Path -LiteralPath $path)) {
        throw "缺少 content-addressed WebFont：$path"
    }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $asset.sha256 -or [int64](Get-Item -LiteralPath $path).Length -ne [int64]$asset.byteLength) {
        throw "WebFont hash 或長度不符合 manifest。"
    }
}

$hashes = @($productManifest.assets | Sort-Object format | ForEach-Object sha256) -join ","
$reproHashes = @($reproManifest.assets | Sort-Object format | ForEach-Object sha256) -join ","
if ($hashes -ne $reproHashes) {
    throw "相同輸入未產生 byte-identical WebFont。"
}

& (Join-Path $PSScriptRoot "Test-WebFontWorkerProcessSmoke.ps1") `
    -FontPath $resolvedFontPath `
    -SourceSha256 $sourceSha256

if (-not [string]::IsNullOrWhiteSpace($MappingTablesRoot)) {
    $mapping = Get-ChildItem -LiteralPath (Join-Path $MappingTablesRoot "Unicode") -File |
        Select-String -Pattern '^3-216F\t201A9$' |
        Select-Object -First 1
    if ($null -eq $mapping) {
        throw "CNS 官方對照表找不到 3-216F → U+201A9。"
    }
}

$projectPath = Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/OdfKit.WebFontSmoke.csproj"
$intermediateRoot = Join-Path $destinationPath "smoke-project-obj"
dotnet restore $projectPath -p:NuGetAudit=false -p:OdfKitWebFontSmokeIntermediateRoot="$intermediateRoot\"
if ($LASTEXITCODE -ne 0) { throw "WebFont smoke 專案還原失敗。" }
dotnet build $projectPath -c Release --no-restore -p:NuGetAudit=false -p:OdfKitWebFontSmokeIntermediateRoot="$intermediateRoot\"
if ($LASTEXITCODE -ne 0) { throw "WebFont smoke 專案建置失敗。" }

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$url = "http://127.0.0.1:$port"
$appDll = Join-Path $repoRoot "tests/OdfKit.WebFontSmoke/bin/Release/net10.0/OdfKit.WebFontSmoke.dll"
$env:ODFKIT_WEBFONT_SMOKE_ASSETS = $assetPath
$env:ODFKIT_WEBFONT_SMOKE_DYNAMIC_SOURCE = $resolvedFontPath
$env:ODFKIT_WEBFONT_SMOKE_DYNAMIC_SOURCE_SHA256 = $sourceSha256
$env:ODFKIT_WEBFONT_SMOKE_DYNAMIC_API_KEY = [Guid]::NewGuid().ToString("N")
Remove-Item -LiteralPath (Join-Path $assetPath "dynamic-cache") -Recurse -Force -ErrorAction SilentlyContinue
$startParameters = @{
    FilePath = "dotnet"
    ArgumentList = @("`"$appDll`"", "--urls", $url)
    PassThru = $true
}
if ($IsWindows) { $startParameters.WindowStyle = "Hidden" }
$process = Start-Process @startParameters

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($process.HasExited) { throw "WebFont smoke 伺服器提前結束。" }
        try { $health = Invoke-RestMethod -Uri "$url/health"; break }
        catch { Start-Sleep -Milliseconds 250 }
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($health.status -ne "ok" -or $health.assetCount -ne 3) {
        throw "WebFont smoke 健康檢查失敗。"
    }

    foreach ($asset in $productManifest.assets) {
        $response = Invoke-WebRequest -Uri "$url/_odf-fonts/$($asset.sha256)/$($asset.fileName)"
        if ([int64]$response.RawContentLength -ne [int64]$asset.byteLength `
            -or $response.Headers["Cache-Control"] -notmatch "immutable") {
            throw "靜態 WebFont HTTP cache 或長度驗證失敗。"
        }
    }

    $dynamicRequest = @{
        fontSourceId = "dynamic-smoke"
        faceIndex = 0
        profileId = "dynamic-smoke-v1"
        fontFamily = "OdfKit Dynamic HTTP Smoke"
        sequences = @("A𠆩")
        formats = @("Woff2")
    } | ConvertTo-Json -Depth 4
    $unauthorized = Invoke-WebRequest -Uri "$url/_odf-fonts/generate" -Method Post `
        -ContentType "application/json" -Body $dynamicRequest -SkipHttpErrorCheck
    if ($unauthorized.StatusCode -ne 401) { throw "未授權動態要求未回傳 401。" }
    $dynamicManifest = Invoke-RestMethod -Uri "$url/_odf-fonts/generate" -Method Post `
        -Headers @{ "X-OdfKit-WebFont-Key" = $env:ODFKIT_WEBFONT_SMOKE_DYNAMIC_API_KEY } `
        -ContentType "application/json" -Body $dynamicRequest
    $dynamicAsset = @($dynamicManifest.assets)[0]
    if (@($dynamicManifest.assets).Count -ne 1 -or $dynamicAsset.fileName -notmatch '\.woff2$') {
        throw "動態 managed endpoint 未產生 WOFF2。"
    }

    if ($RunBrowser) {
        $browserProject = Join-Path $repoRoot "tests/OdfKit.WebFontBrowserSmoke/OdfKit.WebFontBrowserSmoke.csproj"
        dotnet build $browserProject -c Release
        if ($LASTEXITCODE -ne 0) { throw "Playwright browser smoke 建置失敗。" }
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
        foreach ($browser in $Browsers) {
            $screenshot = Join-Path $destinationPath "playwright-managed-$browser.png"
            dotnet run --project $browserProject -c Release --no-build -- $url $browser $screenshot
            if ($LASTEXITCODE -ne 0) { throw "Playwright $browser managed WebFont smoke 失敗。" }
        }
    }

    Write-Host "PASS：純 .NET TTF／WOFF／WOFF2、deterministic hash、HTTP 與動態產字驗證成功。"
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
    $process.Dispose()
    Remove-Item Env:ODFKIT_WEBFONT_SMOKE_ASSETS -ErrorAction SilentlyContinue
    Remove-Item Env:ODFKIT_WEBFONT_SMOKE_DYNAMIC_SOURCE -ErrorAction SilentlyContinue
    Remove-Item Env:ODFKIT_WEBFONT_SMOKE_DYNAMIC_SOURCE_SHA256 -ErrorAction SilentlyContinue
    Remove-Item Env:ODFKIT_WEBFONT_SMOKE_DYNAMIC_API_KEY -ErrorAction SilentlyContinue
}
