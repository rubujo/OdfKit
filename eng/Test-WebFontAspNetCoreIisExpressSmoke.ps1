#Requires -Version 7.0
<#
.SYNOPSIS
以 IIS Express 與 ASP.NET Core Module 驗證 ASP.NET Core 動態 WebFont 部署。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$FontPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$SourceSha256,

    [string]$Destination = "artifacts/webfont-aspnetcore-iis-express-smoke",

    [string]$IisExpressPath = "C:\Program Files\IIS Express\iisexpress.exe",

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
    throw "ASP.NET Core IIS Express smoke 字型 SHA-256 不符合鎖定值。"
}
if (-not (Test-Path -LiteralPath $IisExpressPath -PathType Leaf)) {
    throw "找不到 IIS Express：$IisExpressPath"
}
$ancmPath = Join-Path (Split-Path -Parent $IisExpressPath) "Asp.Net Core Module/V2/aspnetcorev2.dll"
if (-not (Test-Path -LiteralPath $ancmPath -PathType Leaf)) {
    throw "IIS Express 缺少 ASP.NET Core Module V2：$ancmPath"
}

$applicationHostSource = Join-Path ([Environment]::GetFolderPath("MyDocuments")) `
    "IISExpress/config/applicationhost.config"
if (-not (Test-Path -LiteralPath $applicationHostSource -PathType Leaf)) {
    throw "找不到 IIS Express 使用者 applicationhost.config：$applicationHostSource"
}
$applicationHostText = Get-Content -LiteralPath $applicationHostSource -Raw
if (-not $applicationHostText.Contains('name="AspNetCoreModuleV2"', [StringComparison]::Ordinal)) {
    throw "IIS Express applicationhost.config 未註冊 AspNetCoreModuleV2。"
}

$projectPath = Join-Path $repoRoot "samples/WebFonts.AspNetCore/OdfKit.WebFonts.AspNetCore.Sample.csproj"
$sampleAssembly = Join-Path $repoRoot `
    "samples/WebFonts.AspNetCore/bin/Release/net10.0/OdfKit.WebFonts.AspNetCore.Sample.dll"
if ($NoBuild -and -not (Test-Path -LiteralPath $sampleAssembly -PathType Leaf)) {
    throw "NoBuild 需要先建置 ASP.NET Core WebFont sample。"
}

Remove-Item -LiteralPath $destinationPath -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$apiKey = [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
$previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousLegacyApiKey = $env:ODFKIT_WEBFONT_API_KEY
$previousApiKey = $env:OdfKit__WebFonts__ApiKey
$previousAssetRoot = $env:OdfKit__WebFonts__AssetRoot
$previousFontPath = $env:OdfKit__WebFonts__FontPath
$previousFontSourceId = $env:OdfKit__WebFonts__FontSourceId
$previousSourceSha256 = $env:OdfKit__WebFonts__SourceSha256
$previousProfileId = $env:OdfKit__WebFonts__ProfileId
$previousFaceIndex = $env:OdfKit__WebFonts__FaceIndex

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

function Test-NoStore {
    param([Parameter(Mandatory)][Net.Http.HttpResponseMessage]$Response, [string]$Description)

    Assert-Condition ($null -ne $Response.Headers.CacheControl -and $Response.Headers.CacheControl.NoStore) `
        "$Description 缺少 Cache-Control: no-store。"
}

function Invoke-HostingModelSmoke {
    param([Parameter(Mandatory)][ValidateSet("InProcess", "OutOfProcess")][string]$HostingModel)

    $modelName = $HostingModel.ToLowerInvariant()
    $modelRoot = Join-Path $destinationPath $modelName
    $publishPath = Join-Path $modelRoot "site"
    $assetRoot = Join-Path $publishPath "wwwroot/_odf-fonts"
    New-Item -ItemType Directory -Path $modelRoot -Force | Out-Null
    $publishArguments = @(
        "publish",
        $projectPath,
        "-c", "Release",
        "-p:NuGetAudit=false",
        "-p:AspNetCoreHostingModel=$HostingModel",
        "-o", $publishPath,
        "--nologo"
    )
    if ($NoBuild) {
        $publishArguments += @("--no-build", "--no-restore")
    }
    $publishOutput = dotnet @publishArguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "ASP.NET Core $HostingModel 發布失敗。$publishOutput"
    }

    $webConfigPath = Join-Path $publishPath "web.config"
    [xml]$webConfig = Get-Content -LiteralPath $webConfigPath -Raw
    $configuredModel = [string]$webConfig.configuration.location.'system.webServer'.aspNetCore.hostingModel
    Assert-Condition ([string]::Equals($configuredModel, $HostingModel, [StringComparison]::OrdinalIgnoreCase)) `
        "ASP.NET Core 發布 web.config 的 hostingModel 不正確。"

    $appSettingsApiKey = if ($HostingModel -eq "InProcess") {
        $apiKey
    }
    else {
        [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
    }
    $appSettingsPath = Join-Path $publishPath "appsettings.IisSmoke.json"
    [ordered]@{
        OdfKit = [ordered]@{
            WebFonts = [ordered]@{
                ApiKey = $appSettingsApiKey
            }
        }
    } | ConvertTo-Json -Depth 4 | Set-Content `
        -LiteralPath $appSettingsPath `
        -Encoding utf8NoBOM

    $env:ASPNETCORE_ENVIRONMENT = "IisSmoke"
    $env:ODFKIT_WEBFONT_API_KEY = $null
    $env:OdfKit__WebFonts__ApiKey = if ($HostingModel -eq "OutOfProcess") { $apiKey } else { $null }
    $env:OdfKit__WebFonts__AssetRoot = $assetRoot
    $env:OdfKit__WebFonts__FontPath = $resolvedFontPath
    $env:OdfKit__WebFonts__FontSourceId = "cns-ext-b"
    $env:OdfKit__WebFonts__SourceSha256 = $actualSourceSha256
    $env:OdfKit__WebFonts__ProfileId = "cns11643-euc-tw-2026-05-05"
    $env:OdfKit__WebFonts__FaceIndex = "0"

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    $baseUri = [Uri]"http://localhost:$port/"
    $applicationHostPath = Join-Path $modelRoot "applicationhost.config"
    Copy-Item -LiteralPath $applicationHostSource -Destination $applicationHostPath
    [xml]$applicationHost = Get-Content -LiteralPath $applicationHostPath -Raw
    $sites = $applicationHost.configuration.'system.applicationHost'.sites
    $site = @($sites.site)[0]
    foreach ($unusedSite in @($sites.site | Select-Object -Skip 1)) {
        $sites.RemoveChild($unusedSite) | Out-Null
    }
    $siteName = "OdfKitWebFonts$HostingModel"
    $site.SetAttribute("name", $siteName)
    $site.SetAttribute("serverAutoStart", "true")
    $site.application.SetAttribute("applicationPool", "Clr4IntegratedAppPool")
    $site.application.virtualDirectory.SetAttribute("physicalPath", $publishPath)
    $site.bindings.binding.SetAttribute("bindingInformation", ":${port}:localhost")
    $applicationHost.Save($applicationHostPath)
    $standardOutputPath = Join-Path $modelRoot "iisexpress.stdout.log"
    $standardErrorPath = Join-Path $modelRoot "iisexpress.stderr.log"
    $process = $null
    $httpHandler = [Net.Http.HttpClientHandler]::new()
    $httpHandler.UseProxy = $false
    $client = [Net.Http.HttpClient]::new($httpHandler)
    $client.Timeout = [TimeSpan]::FromMinutes(3)

    try {
        $process = Start-Process -FilePath $IisExpressPath `
            -ArgumentList @("/config:`"$applicationHostPath`"", "/site:`"$siteName`"", "/systray:false", "/trace:error") `
            -PassThru `
            -WindowStyle Hidden `
            -RedirectStandardOutput $standardOutputPath `
            -RedirectStandardError $standardErrorPath

        $healthResponse = $null
        $lastStartupStatus = $null
        $consecutiveServerErrors = 0
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            if ($process.HasExited) {
                break
            }
            try {
                $candidate = $client.GetAsync([Uri]::new($baseUri, "health")).GetAwaiter().GetResult()
                if ($candidate.StatusCode -eq [Net.HttpStatusCode]::OK) {
                    $healthResponse = $candidate
                    break
                }
                $lastStartupStatus = [int]$candidate.StatusCode
                if ([int]$candidate.StatusCode -ge 500) {
                    $consecutiveServerErrors++
                    $startupBytes = Read-ResponseBytes $candidate
                    [IO.File]::WriteAllBytes((Join-Path $modelRoot "startup-response.html"), $startupBytes)
                }
                else {
                    $consecutiveServerErrors = 0
                }
                $candidate.Dispose()
                if ($consecutiveServerErrors -ge 3) {
                    break
                }
            }
            catch [Net.Http.HttpRequestException] {
                # ANCM 首次啟動時 listener 可能尚未就緒。
            }
            Start-Sleep -Milliseconds 250
        }
        if ($null -eq $healthResponse) {
            $stdout = if (Test-Path -LiteralPath $standardOutputPath) {
                Get-Content -LiteralPath $standardOutputPath -Raw
            }
            else { "" }
            $stderr = if (Test-Path -LiteralPath $standardErrorPath) {
                Get-Content -LiteralPath $standardErrorPath -Raw
            }
            else { "" }
            throw "ASP.NET Core $HostingModel 未由 IIS Express／ANCM 啟動（HTTP $lastStartupStatus）。$stdout$stderr"
        }

        try {
            $healthBytes = Read-ResponseBytes $healthResponse
            $health = [Text.Encoding]::UTF8.GetString($healthBytes) | ConvertFrom-Json
            Assert-Condition ($health.status -eq "ok" -and $health.dynamicGeneration) `
                "ASP.NET Core $HostingModel health contract 不正確。"
            $serverHeader = @($healthResponse.Headers.Server | ForEach-Object Product | ForEach-Object ToString) -join ","
            $expectedServer = if ($HostingModel -eq "InProcess") { "Microsoft-IIS" } else { "Kestrel" }
            Assert-Condition ($serverHeader.Contains($expectedServer, [StringComparison]::OrdinalIgnoreCase)) `
                "ASP.NET Core $HostingModel 的 hosting server 不符合 ANCM 模式。"
            Assert-Condition ($healthResponse.Headers.Contains("Content-Security-Policy")) `
                "ASP.NET Core $HostingModel health 回應缺少 CSP。"
        }
        finally {
            $healthResponse.Dispose()
        }

        $requestBody = @{
            fontSourceId = "cns-ext-b"
            faceIndex = 0
            profileId = "cns11643-euc-tw-2026-05-05"
            fontFamily = "OdfKit ASP.NET Core IIS"
            sequences = @("A𠆩")
            formats = @("Woff2")
        } | ConvertTo-Json -Depth 4

        $unauthorizedContent = [Net.Http.StringContent]::new(
            $requestBody,
            [Text.Encoding]::UTF8,
            "application/json")
        $unauthorizedResponse = $client.PostAsync(
            [Uri]::new($baseUri, "_odf-fonts/generate"),
            $unauthorizedContent).GetAwaiter().GetResult()
        try {
            Assert-Condition ($unauthorizedResponse.StatusCode -eq [Net.HttpStatusCode]::Unauthorized) `
                "ASP.NET Core $HostingModel 未拒絕缺少 API key 的動態要求。"
            Test-NoStore $unauthorizedResponse "ASP.NET Core $HostingModel 未授權回應"
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
                "ASP.NET Core $HostingModel 動態產字失敗：$([int]$generationResponse.StatusCode)。"
            Test-NoStore $generationResponse "ASP.NET Core $HostingModel 成功動態回應"
            $manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json
        }
        finally {
            $generationResponse.Dispose()
            $generationRequest.Dispose()
        }

        for ($requestIndex = 1; $requestIndex -lt 10; $requestIndex++) {
            $allowedRequest = [Net.Http.HttpRequestMessage]::new(
                [Net.Http.HttpMethod]::Post,
                [Uri]::new($baseUri, "_odf-fonts/generate"))
            $allowedRequest.Headers.Add("X-OdfKit-WebFont-Key", $apiKey)
            $allowedRequest.Content = [Net.Http.StringContent]::new(
                $requestBody,
                [Text.Encoding]::UTF8,
                "application/json")
            $allowedResponse = $client.Send($allowedRequest)
            try {
                Assert-Condition ($allowedResponse.StatusCode -eq [Net.HttpStatusCode]::OK) `
                    "ASP.NET Core $HostingModel 在限流門檻前拒絕合法要求。"
            }
            finally {
                $allowedResponse.Dispose()
                $allowedRequest.Dispose()
            }
        }

        $limitedRequest = [Net.Http.HttpRequestMessage]::new(
            [Net.Http.HttpMethod]::Post,
            [Uri]::new($baseUri, "_odf-fonts/generate"))
        $limitedRequest.Headers.Add("X-OdfKit-WebFont-Key", $apiKey)
        $limitedRequest.Content = [Net.Http.StringContent]::new(
            $requestBody,
            [Text.Encoding]::UTF8,
            "application/json")
        $limitedResponse = $client.Send($limitedRequest)
        try {
            Assert-Condition ($limitedResponse.StatusCode -eq [Net.HttpStatusCode]::TooManyRequests) `
                "ASP.NET Core $HostingModel 未在固定窗口門檻回傳 429。"
            Test-NoStore $limitedResponse "ASP.NET Core $HostingModel 限流回應"
        }
        finally {
            $limitedResponse.Dispose()
            $limitedRequest.Dispose()
        }

        Assert-Condition (@($manifest.assets).Count -eq 1) `
            "ASP.NET Core $HostingModel 未產生單一 WOFF2 資產。"
        $asset = @($manifest.assets)[0]
        $assetUri = [Uri]::new($baseUri, "_odf-fonts/$($asset.sha256)/$($asset.fileName)")
        $assetResponse = $client.GetAsync($assetUri).GetAwaiter().GetResult()
        try {
            $assetBytes = Read-ResponseBytes $assetResponse
            $assetHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($assetBytes)).ToLowerInvariant()
            Assert-Condition ($assetResponse.StatusCode -eq [Net.HttpStatusCode]::OK) `
                "ASP.NET Core $HostingModel 未提供動態 WOFF2。"
            Assert-Condition ($assetHash -eq $asset.sha256 -and $assetBytes.Length -eq $asset.byteLength) `
                "ASP.NET Core $HostingModel WOFF2 與 manifest 不一致。"
            Assert-Condition ($assetResponse.Headers.ETag.Tag -eq "`"$($asset.sha256)`"") `
                "ASP.NET Core $HostingModel WOFF2 ETag 不正確。"
            Assert-Condition ($assetResponse.Headers.CacheControl.Public) `
                "ASP.NET Core $HostingModel WOFF2 未宣告 public cache。"
            Assert-Condition ($assetResponse.Headers.CacheControl.Extensions.Name -contains "immutable") `
                "ASP.NET Core $HostingModel WOFF2 缺少 immutable。"
            Assert-Condition ($assetResponse.Headers.Contains("Content-Security-Policy")) `
                "ASP.NET Core $HostingModel WOFF2 回應缺少 CSP。"
            $etag = $assetResponse.Headers.ETag
        }
        finally {
            $assetResponse.Dispose()
        }

        $headRequest = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Head, $assetUri)
        $headResponse = $client.Send($headRequest)
        try {
            $headBytes = Read-ResponseBytes $headResponse
            Assert-Condition ($headResponse.StatusCode -eq [Net.HttpStatusCode]::OK) `
                "ASP.NET Core $HostingModel WOFF2 HEAD 失敗。"
            Assert-Condition ($headBytes.Length -eq 0 -and $headResponse.Content.Headers.ContentLength -eq $asset.byteLength) `
                "ASP.NET Core $HostingModel WOFF2 HEAD 本文或長度不正確。"
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
                "ASP.NET Core $HostingModel WOFF2 If-None-Match 未回傳 304。"
        }
        finally {
            $conditionalResponse.Dispose()
            $conditionalRequest.Dispose()
        }

        $iisTrace = Get-Content -LiteralPath $standardOutputPath -Raw
        Assert-Condition ($iisTrace.Contains("Successfully registered URL `"$baseUri`"", [StringComparison]::Ordinal)) `
            "ASP.NET Core $HostingModel 缺少 IIS Express listener 證據。"
        Assert-Condition ($iisTrace.Contains("Response sent: $baseUri", [StringComparison]::Ordinal)) `
            "ASP.NET Core $HostingModel 缺少 IIS Express proxy 回應證據。"

        return [ordered]@{
            hostingModel = $HostingModel
            apiKeySource = if ($HostingModel -eq "InProcess") { "appsettings.IisSmoke.json" } else { "environment-override" }
            server = $serverHeader
            profileId = $manifest.profileId
            sourceSha256 = $actualSourceSha256
            webConfigSha256 = (Get-FileHash -LiteralPath $webConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
            asset = [ordered]@{
                format = "Woff2"
                fileName = $asset.fileName
                sha256 = $asset.sha256
                byteLength = $asset.byteLength
            }
        }
    }
    finally {
        $client.Dispose()
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit(5000) | Out-Null
        }
        Remove-Item -LiteralPath $appSettingsPath -Force -ErrorAction SilentlyContinue
    }
}

try {
    $evidence = @(
        Invoke-HostingModelSmoke -HostingModel InProcess
        Invoke-HostingModelSmoke -HostingModel OutOfProcess
    )
    [ordered]@{
        server = "IIS Express"
        aspNetCoreModuleVersion = (Get-Item -LiteralPath $ancmPath).VersionInfo.FileVersion
        targetFramework = "net10.0"
        models = $evidence
    } | ConvertTo-Json -Depth 8 | Set-Content `
        -LiteralPath (Join-Path $destinationPath "evidence.json") `
        -Encoding utf8NoBOM
    Write-Host "PASS: IIS Express／ANCM 實際處理 ASP.NET Core InProcess 與 OutOfProcess 動態產字。"
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    $env:ODFKIT_WEBFONT_API_KEY = $previousLegacyApiKey
    $env:OdfKit__WebFonts__ApiKey = $previousApiKey
    $env:OdfKit__WebFonts__AssetRoot = $previousAssetRoot
    $env:OdfKit__WebFonts__FontPath = $previousFontPath
    $env:OdfKit__WebFonts__FontSourceId = $previousFontSourceId
    $env:OdfKit__WebFonts__SourceSha256 = $previousSourceSha256
    $env:OdfKit__WebFonts__ProfileId = $previousProfileId
    $env:OdfKit__WebFonts__FaceIndex = $previousFaceIndex
}
