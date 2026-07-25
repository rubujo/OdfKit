#Requires -Version 7.0
<#
.SYNOPSIS
    發布並透過真實 Windows SCM 驗證 NativeAOT WebFont Sidecar。
.PARAMETER Configuration
    建置組態，預設為 Release。
.PARAMETER FontPath
    用於實際產生 WOFF2 的 OpenType 字型。
.PARAMETER HostExecutablePath
    已發布的 win-x64 NativeAOT Host；未指定時由本腳本發布。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$FontPath,
    [string]$HostExecutablePath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$serviceName = "OdfKitWebFontSmoke" + [guid]::NewGuid().ToString("N").Substring(0, 12)
$pipeName = "odfkit-webfont-service-" + [guid]::NewGuid().ToString("N")
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("odfkit-webfont-service-" + [guid]::NewGuid().ToString("N"))
$publishRoot = Join-Path $testRoot "host"
$assetRoot = Join-Path $testRoot "assets"
$cacheRoot = Join-Path $testRoot "cache"
$tokenFile = Join-Path $testRoot "sidecar.token"
$smokeArtifacts = Join-Path $testRoot "build"
$installed = $false

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "真實 SCM smoke 必須在系統管理員 PowerShell 中執行。"
}

if ([string]::IsNullOrWhiteSpace($FontPath)) {
    $fontCandidates = @(
        (Join-Path $env:WINDIR "Fonts/arial.ttf"),
        (Join-Path $env:WINDIR "Fonts/calibri.ttf"),
        (Join-Path $env:WINDIR "Fonts/segoeui.ttf")
    )
    $FontPath = $fontCandidates | Where-Object {
        Test-Path -LiteralPath $_ -PathType Leaf
    } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($FontPath) -or -not (Test-Path -LiteralPath $FontPath -PathType Leaf)) {
    throw "找不到可供 Windows Service smoke 使用的 OpenType 字型。"
}
$FontPath = (Resolve-Path -LiteralPath $FontPath).Path

New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
try {
    Push-Location $repoRoot
    try {
        if ([string]::IsNullOrWhiteSpace($HostExecutablePath)) {
            dotnet restore OdfKit.WebFonts.Sidecar.Host/OdfKit.WebFonts.Sidecar.Host.csproj -r win-x64
            if ($LASTEXITCODE -ne 0) {
                throw "Sidecar Host 還原失敗。"
            }
            dotnet publish OdfKit.WebFonts.Sidecar.Host/OdfKit.WebFonts.Sidecar.Host.csproj `
                -c $Configuration `
                -r win-x64 `
                --no-restore `
                -o $publishRoot
            if ($LASTEXITCODE -ne 0) {
                throw "Sidecar Host NativeAOT 發布失敗。"
            }
            $HostExecutablePath = Join-Path $publishRoot "OdfKit.WebFonts.Sidecar.Host.exe"
        }
        dotnet build tests/OdfKit.WebFonts.Sidecar.Net48Smoke/OdfKit.WebFonts.Sidecar.Net48Smoke.csproj `
            -c $Configuration `
            --artifacts-path $smokeArtifacts
        if ($LASTEXITCODE -ne 0) {
            throw "net48 Sidecar smoke 建置失敗。"
        }
    }
    finally {
        Pop-Location
    }

    if (-not [IO.Path]::IsPathFullyQualified($HostExecutablePath)) {
        $HostExecutablePath = Join-Path $repoRoot $HostExecutablePath
    }
    $hostExecutable = (Resolve-Path -LiteralPath $HostExecutablePath).Path
    & (Join-Path $PSScriptRoot "Manage-WebFontSidecarService.ps1") `
        -Action Install `
        -ServiceName $serviceName `
        -DisplayName "OdfKit WebFont Sidecar Smoke" `
        -HostExecutablePath $hostExecutable `
        -PipeName $pipeName `
        -AssetRootPath $assetRoot `
        -CacheRootPath $cacheRoot `
        -TokenFilePath $tokenFile `
        -FontSource "smoke-source=$FontPath" `
        -StartService `
        -Confirm:$false
    $installed = $true

    $service = Get-Service -Name $serviceName -ErrorAction Stop
    if ($service.Status -ne "Running") {
        throw "Sidecar Windows Service 未進入 Running。"
    }

    $token = (Get-Content -LiteralPath $tokenFile -Raw).Trim()
    $previousToken = [Environment]::GetEnvironmentVariable(
        "ODFKIT_WEBFONT_SIDECAR_TOKEN",
        "Process")
    [Environment]::SetEnvironmentVariable(
        "ODFKIT_WEBFONT_SIDECAR_TOKEN",
        $token,
        "Process")
    try {
        $smokeExecutable = Get-ChildItem -LiteralPath $smokeArtifacts -Recurse `
            -Filter "OdfKit.WebFonts.Sidecar.Net48Smoke.exe" -File |
            Select-Object -First 1 -ExpandProperty FullName
        if ([string]::IsNullOrWhiteSpace($smokeExecutable)) {
            throw "找不到 net48 Sidecar smoke 執行檔。"
        }

        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        do {
            Start-Sleep -Milliseconds 200
            & $smokeExecutable `
                --pipe $pipeName `
                --asset-root $assetRoot `
                --font $FontPath
            $smokeExitCode = $LASTEXITCODE
        } while ($smokeExitCode -ne 0 -and [DateTime]::UtcNow -lt $deadline)
        if ($smokeExitCode -ne 0) {
            throw "net48 無法透過 Windows Service Sidecar 產生 WOFF2。"
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            "ODFKIT_WEBFONT_SIDECAR_TOKEN",
            $previousToken,
            "Process")
    }

    & "$env:SystemRoot\System32\sc.exe" stop $serviceName | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 200
        $service = Get-Service -Name $serviceName -ErrorAction Stop
    } while ($service.Status -ne "Stopped" -and [DateTime]::UtcNow -lt $deadline)
    if ($service.Status -ne "Stopped") {
        throw "Sidecar Windows Service 無法正常停止。"
    }

    & "$env:SystemRoot\System32\sc.exe" start $serviceName | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 200
        $service = Get-Service -Name $serviceName -ErrorAction Stop
    } while ($service.Status -ne "Running" -and [DateTime]::UtcNow -lt $deadline)
    if ($service.Status -ne "Running") {
        throw "Sidecar Windows Service 無法重新啟動。"
    }

    Write-Host "NativeAOT Sidecar Windows Service SCM smoke 通過。"
}
finally {
    & "$env:SystemRoot\System32\sc.exe" query $serviceName *> $null
    $serviceExists = $LASTEXITCODE -eq 0
    if ($installed -or $serviceExists) {
        & (Join-Path $PSScriptRoot "Manage-WebFontSidecarService.ps1") `
            -Action Uninstall `
            -ServiceName $serviceName `
            -Confirm:$false
    }
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
        $expectedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedTestRoot.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "拒絕清理非暫存目錄：$resolvedTestRoot"
        }
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
