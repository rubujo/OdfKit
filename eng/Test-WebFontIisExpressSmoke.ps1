#Requires -Version 7.0
<#
.SYNOPSIS
以真實 IIS Express 與鎖定字型驗證 ASP.NET Web Forms 動態 WebFont 部署。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$FontPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$SourceSha256,

    [string]$Destination = "artifacts/webfont-iis-express-smoke",

    [string]$IisExpressPath = "C:\Program Files\IIS Express\iisexpress.exe",

    [ValidateSet("Integrated", "Classic")]
    [string]$Pipeline = "Integrated",

    [switch]$NoBuild
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

$resolvedFontPath = (Resolve-Path -LiteralPath $FontPath).Path
$actualSourceSha256 = (Get-FileHash -LiteralPath $resolvedFontPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSourceSha256 -ne $SourceSha256.ToLowerInvariant()) {
    throw "IIS Express smoke 字型 SHA-256 不符合鎖定值。"
}
if (-not (Test-Path -LiteralPath $IisExpressPath -PathType Leaf)) {
    throw "找不到 IIS Express：$IisExpressPath"
}
$applicationHostSource = Join-Path ([Environment]::GetFolderPath("MyDocuments")) `
    "IISExpress/config/applicationhost.config"
if (-not (Test-Path -LiteralPath $applicationHostSource -PathType Leaf)) {
    throw "找不到 IIS Express 使用者 applicationhost.config：$applicationHostSource"
}
$applicationPool = if ($Pipeline -eq "Classic") { "Clr4ClassicAppPool" } else { "Clr4IntegratedAppPool" }
$applicationHostText = Get-Content -LiteralPath $applicationHostSource -Raw
if (-not $applicationHostText.Contains("name=`"$applicationPool`"", [StringComparison]::Ordinal)) {
    throw "IIS Express applicationhost.config 缺少 $applicationPool。"
}

$projectPath = Join-Path $repoRoot "OdfKit.WebFonts.Hosting.SystemWeb/OdfKit.WebFonts.Hosting.SystemWeb.csproj"
$buildOutput = Join-Path $repoRoot "OdfKit.WebFonts.Hosting.SystemWeb/bin/Release/net48"
if (-not $NoBuild) {
    dotnet build $projectPath -c Release -f net48 --nologo -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) {
        throw "System.Web WebFont 套件建置失敗。"
    }
}
elseif (-not (Test-Path -LiteralPath (Join-Path $buildOutput "OdfKit.WebFonts.Hosting.SystemWeb.dll"))) {
    throw "NoBuild 需要先建置 System.Web WebFont 套件。"
}

Remove-Item -LiteralPath $destinationPath -Recurse -Force -ErrorAction SilentlyContinue
$sitePath = Join-Path $destinationPath "site"
$binPath = Join-Path $sitePath "bin"
$appDataPath = Join-Path $sitePath "App_Data"
$fontDirectory = Join-Path $appDataPath "Fonts"
New-Item -ItemType Directory -Path $binPath, $fontDirectory -Force | Out-Null

Get-ChildItem -LiteralPath $buildOutput -File |
    Where-Object Extension -In ".dll", ".pdb" |
    Copy-Item -Destination $binPath -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "samples/WebFonts.WebForms/Default.aspx") -Destination $sitePath
Copy-Item -LiteralPath (Join-Path $repoRoot "samples/WebFonts.WebForms/Web.config") -Destination $sitePath

$fontFileName = [IO.Path]::GetFileName($resolvedFontPath)
Copy-Item -LiteralPath $resolvedFontPath -Destination (Join-Path $fontDirectory $fontFileName)
$apiKey = [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
$dynamicConfiguration = @{
    schemaVersion = 1
    assetRootPath = "OdfWebFonts"
    apiKeyEnvironmentVariable = "ODFKIT_WEBFONT_API_KEY"
    apiKeyAppSettingName = "OdfKit.WebFonts.ApiKey"
    maxRequestBodyBytes = 65536
    maxConcurrentGenerations = 2
    maxSequenceCount = 256
    maxUnicodeScalarCount = 4096
    maxAssetBytes = 33554432
    allowPublicCrossOriginAssets = $false
    fontSources = @(
        @{
            id = "cns-ext-b"
            path = "Fonts/$fontFileName"
            sha256 = $actualSourceSha256
            faceIndex = 0
            fontFamily = "OdfKit CNS Ext-B"
        }
    )
    allowedProfileIds = @("cns11643-euc-tw-2026-05-05")
    allowedFormats = @("Woff", "TrueType")
}
$dynamicConfiguration |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $appDataPath "webfonts.dynamic.json") -Encoding utf8NoBOM

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$baseUri = [Uri]"http://localhost:$port/"
$standardOutputPath = Join-Path $destinationPath "iisexpress.stdout.log"
$standardErrorPath = Join-Path $destinationPath "iisexpress.stderr.log"
$previousApiKey = $env:ODFKIT_WEBFONT_API_KEY
$env:ODFKIT_WEBFONT_API_KEY = $null
[xml]$siteWebConfig = Get-Content -LiteralPath (Join-Path $sitePath "Web.config") -Raw
$apiKeySetting = @(@($siteWebConfig.configuration.appSettings.add) |
        Where-Object key -EQ "OdfKit.WebFonts.ApiKey")
if ($apiKeySetting.Count -ne 1) {
    throw "Web Forms sample 缺少 web.config API key 設定。"
}
$apiKeySetting[0].SetAttribute("value", $apiKey)
$siteWebConfig.Save((Join-Path $sitePath "Web.config"))
$applicationHostPath = Join-Path $destinationPath "applicationhost.config"
Copy-Item -LiteralPath $applicationHostSource -Destination $applicationHostPath
[xml]$applicationHost = Get-Content -LiteralPath $applicationHostPath -Raw
$sites = $applicationHost.configuration.'system.applicationHost'.sites
$site = @($sites.site)[0]
foreach ($unusedSite in @($sites.site | Select-Object -Skip 1)) {
    $sites.RemoveChild($unusedSite) | Out-Null
}
$siteName = "OdfKitWebFontsWebForms$Pipeline"
$site.SetAttribute("name", $siteName)
$site.SetAttribute("serverAutoStart", "true")
$site.application.SetAttribute("applicationPool", $applicationPool)
$site.application.virtualDirectory.SetAttribute("physicalPath", $sitePath)
$site.bindings.binding.SetAttribute("bindingInformation", ":${port}:localhost")
$applicationHost.Save($applicationHostPath)
$process = $null
$httpHandler = [Net.Http.HttpClientHandler]::new()
$httpHandler.UseProxy = $false
$client = [Net.Http.HttpClient]::new($httpHandler)
$client.Timeout = [TimeSpan]::FromSeconds(30)

function Assert-Condition {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

function Read-ResponseBytes {
    param([Parameter(Mandatory)][Net.Http.HttpResponseMessage]$Response)

    return $Response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
}

try {
    $process = Start-Process -FilePath $IisExpressPath `
        -ArgumentList @("/config:`"$applicationHostPath`"", "/site:`"$siteName`"", "/systray:false", "/trace:error") `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $standardOutputPath `
        -RedirectStandardError $standardErrorPath

    $started = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) {
            break
        }
        try {
            $probe = $client.GetAsync($baseUri).GetAwaiter().GetResult()
            $probe.Dispose()
            $started = $true
            break
        }
        catch [Net.Http.HttpRequestException] {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $started) {
        $stderr = if (Test-Path -LiteralPath $standardErrorPath) {
            Get-Content -LiteralPath $standardErrorPath -Raw
        }
        else { "" }
        throw "IIS Express 未在期限內啟動。$stderr"
    }

    $pageResponse = $client.GetAsync($baseUri).GetAwaiter().GetResult()
    try {
        $pageBytes = Read-ResponseBytes $pageResponse
        Assert-Condition ($pageResponse.StatusCode -eq [Net.HttpStatusCode]::OK) `
            "Web Forms 頁面未由 IIS Express 成功編譯：$([int]$pageResponse.StatusCode)。"
        $pageText = [Text.Encoding]::UTF8.GetString($pageBytes)
        Assert-Condition ($pageText.Contains("/_odf-fonts/webfonts.css", [StringComparison]::Ordinal)) `
            "Web Forms 頁面未輸出 WebFont stylesheet link。"
    }
    finally {
        $pageResponse.Dispose()
    }

    $requestBody = @{
        fontSourceId = "cns-ext-b"
        faceIndex = 0
        profileId = "cns11643-euc-tw-2026-05-05"
        fontFamily = "OdfKit CNS Ext-B"
        sequences = @("A𠆩")
        formats = @("Woff", "TrueType")
    } | ConvertTo-Json -Depth 4

    $unauthorizedContent = [Net.Http.StringContent]::new($requestBody, [Text.Encoding]::UTF8, "application/json")
    $unauthorizedResponse = $client.PostAsync(
        [Uri]::new($baseUri, "_odf-fonts/generate"),
        $unauthorizedContent).GetAwaiter().GetResult()
    try {
        Assert-Condition ($unauthorizedResponse.StatusCode -eq [Net.HttpStatusCode]::Unauthorized) `
            "IIS Express 動態 endpoint 未拒絕缺少 API key 的要求。"
        Assert-Condition ($unauthorizedResponse.Headers.CacheControl.NoStore) `
            "IIS Express 未授權動態回應缺少 no-store。"
    }
    finally {
        $unauthorizedResponse.Dispose()
        $unauthorizedContent.Dispose()
    }

    $generationRequest = [Net.Http.HttpRequestMessage]::new(
        [Net.Http.HttpMethod]::Post,
        [Uri]::new($baseUri, "_odf-fonts/generate"))
    $generationRequest.Headers.Add("X-OdfKit-WebFont-Key", $apiKey)
    $generationRequest.Content = [Net.Http.StringContent]::new(
        $requestBody,
        [Text.Encoding]::UTF8,
        "application/json")
    $generationResponse = $client.Send($generationRequest)
    try {
        $manifestBytes = Read-ResponseBytes $generationResponse
        Assert-Condition ($generationResponse.StatusCode -eq [Net.HttpStatusCode]::OK) `
            "IIS Express 動態產字失敗：$([int]$generationResponse.StatusCode)。"
        Assert-Condition ($generationResponse.Headers.CacheControl.NoStore) `
            "IIS Express 成功動態回應缺少 no-store。"
        $manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json
    }
    finally {
        $generationResponse.Dispose()
        $generationRequest.Dispose()
    }

    Assert-Condition (@($manifest.Assets).Count -eq 2) "IIS Express 動態產字未回傳兩種格式。"
    foreach ($asset in @($manifest.Assets)) {
        $assetUri = [Uri]::new($baseUri, "_odf-fonts/$($asset.Sha256)/$($asset.FileName)")
        $assetResponse = $client.GetAsync($assetUri).GetAwaiter().GetResult()
        try {
            $assetBytes = Read-ResponseBytes $assetResponse
            $assetHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($assetBytes)).ToLowerInvariant()
            Assert-Condition ($assetResponse.StatusCode -eq [Net.HttpStatusCode]::OK) `
                "IIS Express 未提供動態產生的內容定址資產。"
            Assert-Condition ($assetHash -eq $asset.Sha256) "IIS Express 資產 SHA-256 與 manifest 不一致。"
            Assert-Condition ($assetBytes.Length -eq $asset.ByteLength) "IIS Express 資產長度與 manifest 不一致。"
            Assert-Condition ($assetResponse.Headers.ETag.Tag -eq "`"$($asset.Sha256)`"") `
                "IIS Express 資產 ETag 不正確。"
            Assert-Condition ($assetResponse.Headers.CacheControl.Public) "IIS Express 資產未宣告 public cache。"
            Assert-Condition ($assetResponse.Headers.CacheControl.Extensions.Name -contains "immutable") `
                "IIS Express 資產缺少 immutable cache extension。"
            $etag = $assetResponse.Headers.ETag
        }
        finally {
            $assetResponse.Dispose()
        }

        $headRequest = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Head, $assetUri)
        $headResponse = $client.Send($headRequest)
        try {
            $headBytes = Read-ResponseBytes $headResponse
            Assert-Condition ($headResponse.StatusCode -eq [Net.HttpStatusCode]::OK) "IIS Express 資產 HEAD 失敗。"
            Assert-Condition ($headBytes.Length -eq 0) "IIS Express 資產 HEAD 不應回傳 body。"
            Assert-Condition ($headResponse.Content.Headers.ContentLength -eq $asset.ByteLength) `
                "IIS Express 資產 HEAD Content-Length 不正確。"
        }
        finally {
            $headResponse.Dispose()
            $headRequest.Dispose()
        }

        $conditionalRequest = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $assetUri)
        $conditionalRequest.Headers.IfNoneMatch.Add($etag)
        $conditionalResponse = $client.Send($conditionalRequest)
        try {
            Assert-Condition ($conditionalResponse.StatusCode -eq [Net.HttpStatusCode]::NotModified) `
                "IIS Express 資產 If-None-Match 未回傳 304。"
        }
        finally {
            $conditionalResponse.Dispose()
            $conditionalRequest.Dispose()
        }
    }

    [ordered]@{
        server = "IIS Express"
        runtime = ".NET Framework 4.8"
        pipeline = $Pipeline
        profileId = $manifest.ProfileId
        sourceSha256 = $actualSourceSha256
        assets = @($manifest.Assets | ForEach-Object {
            [ordered]@{
                format = $_.Format
                fileName = $_.FileName
                sha256 = $_.Sha256
                byteLength = $_.ByteLength
            }
        })
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destinationPath "evidence.json") -Encoding utf8NoBOM

    Write-Host "PASS: IIS Express $Pipeline pipeline 實際處理 Web Forms 動態產字、內容定址快取與條件式要求。"
}
finally {
    $client.Dispose()
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(5000) | Out-Null
    }
    $apiKeySetting[0].SetAttribute("value", "")
    $siteWebConfig.Save((Join-Path $sitePath "Web.config"))
    $env:ODFKIT_WEBFONT_API_KEY = $previousApiKey
}
