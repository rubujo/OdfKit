#Requires -Version 7.0
<#
.SYNOPSIS
    將已驗證的 OdfKit NuGet 套件推送至 nuget.org（REL-1 發佈流程）。
.DESCRIPTION
    預設為乾跑：先執行 Test-NuGetPack.ps1，列出將推送的套件。
    使用 -Push 與 NUGET_API_KEY 環境變數（或 -ApiKey）才會實際上傳。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Source
    NuGet feed URL，預設 nuget.org。
.PARAMETER ApiKey
    API 金鑰；未指定時讀取環境變數 NUGET_API_KEY。
.PARAMETER Push
    實際推送套件（含 .snupkg）。
.PARAMETER SkipValidation
    略過 Test-NuGetPack.ps1（僅建議本機重複推送時使用）。
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Configuration = "Release",
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = $(if ($env:NUGET_API_KEY) { $env:NUGET_API_KEY } else { "" }),
    [switch]$Push,
    [switch]$SkipValidation
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot "artifacts/nuget"

Push-Location $repoRoot
try {
    if (-not $SkipValidation) {
        & (Join-Path $PSScriptRoot "Test-NuGetPack.ps1") -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $packages = @(
        Get-ChildItem -LiteralPath $outDir -Filter *.nupkg -File
        Get-ChildItem -LiteralPath $outDir -Filter *.snupkg -File
    )
    if ($packages.Count -eq 0) {
        throw "找不到套件：$outDir"
    }

    Write-Host ""
    Write-Host "將推送 $($packages.Count) 個檔案至：$Source"
    foreach ($pkg in $packages) {
        Write-Host "  $($pkg.Name)"
    }

    if (-not $Push) {
        Write-Host ""
        Write-Host "乾跑完成。若要實際推送："
        Write-Host "  `$env:NUGET_API_KEY = '<your-key>'"
        Write-Host "  pwsh eng/Publish-NuGet.ps1 -Push"
        return
    }

    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        throw "推送需要 API 金鑰：設定 NUGET_API_KEY 環境變數或傳入 -ApiKey。"
    }

    foreach ($pkg in $packages) {
        if ($PSCmdlet.ShouldProcess($pkg.FullName, "nuget push")) {
            Write-Host "推送：$($pkg.Name)"
            dotnet nuget push $pkg.FullName --source $Source --api-key $ApiKey --skip-duplicate
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
    }

    Write-Host ""
    Write-Host "NuGet 推送完成。"
}
finally {
    Pop-Location
}