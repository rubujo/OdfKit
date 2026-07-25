#Requires -Version 7.0
<#
.SYNOPSIS
    發布 NativeAOT WebFont sidecar，並以 net48 用戶端產生真正的 WOFF2。
.PARAMETER Configuration
    建置組態，預設為 Release。
.PARAMETER RuntimeIdentifier
    Windows NativeAOT RID，預設為 win-x64。
.PARAMETER FontPath
    用於實際子集化的 OpenType 字型；未指定時會探測 Windows Fonts。
.PARAMETER PublishOnly
    只交叉發布目標架構，不在目前主機執行產物。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeIdentifier = "win-x64",
    [string]$FontPath,
    [switch]$PublishOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $repoRoot "OdfKit.WebFonts.Sidecar.Host/OdfKit.WebFonts.Sidecar.Host.csproj"
$smokeProject = Join-Path $repoRoot "tests/OdfKit.WebFonts.Sidecar.Net48Smoke/OdfKit.WebFonts.Sidecar.Net48Smoke.csproj"
$systemWebSmokeProject = Join-Path $repoRoot "tests/OdfKit.WebFonts.SystemWebSmoke/OdfKit.WebFonts.SystemWebSmoke.csproj"
$publishRoot = Join-Path $repoRoot "artifacts/webfont-sidecar-aot-$RuntimeIdentifier"
$assetRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("odfkit-sidecar-assets-" + [guid]::NewGuid().ToString("N"))
$hostArtifacts = Join-Path ([System.IO.Path]::GetTempPath()) ("odfkit-sidecar-host-" + [guid]::NewGuid().ToString("N"))
$smokeArtifacts = Join-Path ([System.IO.Path]::GetTempPath()) ("odfkit-sidecar-build-" + [guid]::NewGuid().ToString("N"))
$pipeName = "odfkit-webfont-" + [guid]::NewGuid().ToString("N")
$token = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$hostProcess = $null
$succeeded = $false

if ([string]::IsNullOrWhiteSpace($FontPath)) {
    $fontCandidates = @(
        (Join-Path $env:WINDIR "Fonts/arial.ttf"),
        (Join-Path $env:WINDIR "Fonts/calibri.ttf"),
        (Join-Path $env:WINDIR "Fonts/segoeui.ttf")
    )
    $FontPath = $fontCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($FontPath) -or -not (Test-Path -LiteralPath $FontPath -PathType Leaf)) {
    throw "找不到可供 sidecar 實測的 OpenType 字型。"
}
$FontPath = (Resolve-Path -LiteralPath $FontPath).Path

Push-Location $repoRoot
try {
    dotnet restore $hostProject -r $RuntimeIdentifier --artifacts-path $hostArtifacts
    if ($LASTEXITCODE -ne 0) {
        throw "NativeAOT sidecar 還原失敗，結束碼 $LASTEXITCODE。"
    }

    dotnet publish $hostProject `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --no-restore `
        --artifacts-path $hostArtifacts `
        -o $publishRoot
    if ($LASTEXITCODE -ne 0) {
        throw "NativeAOT sidecar 發布失敗，結束碼 $LASTEXITCODE。"
    }

    $hostExecutable = Join-Path $publishRoot "OdfKit.WebFonts.Sidecar.Host.exe"
    if (-not (Test-Path -LiteralPath $hostExecutable -PathType Leaf)) {
        throw "找不到 NativeAOT sidecar 執行檔。"
    }
    if ($PublishOnly) {
        Write-Host "NativeAOT sidecar 交叉發布通過（$RuntimeIdentifier）。"
        return
    }

    $probe = & $hostExecutable --probe
    if ($LASTEXITCODE -ne 0 -or $probe -notmatch "protocol=1;woff2=True;rid=$([regex]::Escape($RuntimeIdentifier))") {
        throw "NativeAOT sidecar 能力探測失敗：$probe"
    }

    dotnet build $smokeProject -c $Configuration --artifacts-path $smokeArtifacts
    if ($LASTEXITCODE -ne 0) {
        throw "net48 sidecar smoke 建置失敗，結束碼 $LASTEXITCODE。"
    }
    dotnet build $systemWebSmokeProject -c $Configuration --artifacts-path $smokeArtifacts
    if ($LASTEXITCODE -ne 0) {
        throw "System.Web sidecar smoke 建置失敗，結束碼 $LASTEXITCODE。"
    }

    New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
    $previousToken = [Environment]::GetEnvironmentVariable("ODFKIT_WEBFONT_SIDECAR_TOKEN", "Process")
    [Environment]::SetEnvironmentVariable("ODFKIT_WEBFONT_SIDECAR_TOKEN", $token, "Process")
    try {
        $hostProcess = Start-Process -FilePath $hostExecutable `
            -ArgumentList @(
                "--pipe", $pipeName,
                "--asset-root", $assetRoot,
                "--font-source", "smoke-source=$FontPath",
                "--max-concurrency", "1",
                "--queue-capacity", "4") `
            -PassThru `
            -WindowStyle Hidden

        $smokeExecutable = Get-ChildItem -LiteralPath $smokeArtifacts -Recurse `
            -Filter "OdfKit.WebFonts.Sidecar.Net48Smoke.exe" -File |
            Select-Object -First 1 -ExpandProperty FullName
        if ([string]::IsNullOrWhiteSpace($smokeExecutable)) {
            throw "找不到 net48 sidecar smoke 執行檔。"
        }
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            Start-Sleep -Milliseconds 100
            if ($hostProcess.HasExited) {
                throw "NativeAOT sidecar 在 smoke 連線前異常結束，結束碼 $($hostProcess.ExitCode)。"
            }
            try {
                & $smokeExecutable `
                    --pipe $pipeName `
                    --asset-root $assetRoot `
                    --font $FontPath
                $smokeExitCode = $LASTEXITCODE
            }
            catch {
                $smokeExitCode = 1
            }
        } while ($smokeExitCode -ne 0 -and [DateTime]::UtcNow -lt $deadline)

        if ($smokeExitCode -ne 0) {
            throw "net48 至 NativeAOT sidecar 的 WOFF2 實測失敗。"
        }

        Remove-Item -LiteralPath $assetRoot -Recurse -Force
        New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
        $fontSha256 = (Get-FileHash -LiteralPath $FontPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $systemWebSmokeExecutable = Get-ChildItem -LiteralPath $smokeArtifacts -Recurse `
            -Filter "OdfKit.WebFonts.SystemWebSmoke.exe" -File |
            Select-Object -First 1 -ExpandProperty FullName
        if ([string]::IsNullOrWhiteSpace($systemWebSmokeExecutable)) {
            throw "找不到 System.Web sidecar smoke 執行檔。"
        }
        & $systemWebSmokeExecutable `
            --font $FontPath `
            --sha256 $fontSha256 `
            --text "OdfKit" `
            --asset-root $assetRoot `
            --sidecar-pipe $pipeName `
            --sidecar-only
        if ($LASTEXITCODE -ne 0) {
            throw "System.Web 至 NativeAOT sidecar 的 WOFF2 實測失敗，結束碼 $LASTEXITCODE。"
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable("ODFKIT_WEBFONT_SIDECAR_TOKEN", $previousToken, "Process")
    }

    Write-Host "NativeAOT sidecar 與 net48 WOFF2 實測通過（$RuntimeIdentifier）。"
    $succeeded = $true
}
finally {
    if ($null -ne $hostProcess -and -not $hostProcess.HasExited) {
        Stop-Process -Id $hostProcess.Id -Force
        $hostProcess.WaitForExit()
    }
    if (Test-Path -LiteralPath $assetRoot) {
        Remove-Item -LiteralPath $assetRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $smokeArtifacts) {
        Remove-Item -LiteralPath $smokeArtifacts -Recurse -Force
    }
    if (Test-Path -LiteralPath $hostArtifacts) {
        Remove-Item -LiteralPath $hostArtifacts -Recurse -Force
    }
    Pop-Location
}

if ($succeeded) {
    exit 0
}
