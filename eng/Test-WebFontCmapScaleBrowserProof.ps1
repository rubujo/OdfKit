#requires -Version 7.0
<#
.SYNOPSIS
    以真實字型與三個瀏覽器引擎驗證 cmap format 4 的規模路徑。

.DESCRIPTION
    涵蓋兩條先前只有 managed verifier 證據、缺少實機證據的路徑：

      dense  — 單片子集超過 8,188 個 BMP 字元。此規模在 format 4 範圍合併之前
               必定以 cmap4-size 失敗，因此既有的 256 code-point bucket 證據
               無法涵蓋。
      sparse — 合併後 segment 數仍超過 format 4 的 16-bit length 上限，依
               OpenType 1.9.1 省略 format 4，只保留 (3,10)／format 12。

    另包含一個負向對照：同一資產截斷後供應，三個引擎都必須拒絕。若對照通過，
    表示量測本身無效，正向案例的結果也不可採信。
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot ".." "artifacts" "cmap-scale-proof"),

    # 既有 WebFont 閘門已把同一支鎖定字型下載並快取；指向該檔即可避免重複下載。
    [string]$SourceFontPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$destinationPath = [IO.Path]::GetFullPath($Destination)
$repoPrefix = $repoRoot.Path.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $destinationPath.StartsWith($repoPrefix, $comparison)) {
    throw "Destination 必須位於方案目錄內。"
}

$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "external-tools.json") -Raw | ConvertFrom-Json
$definition = $manifest.webFontSmoke.internationalFonts.cjkOpenType
$sourceRoot = Join-Path $destinationPath "sources"
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
$fontPath = Join-Path $sourceRoot $definition.fileName

# 若呼叫端提供了既有的鎖定字型（CI 由 format matrix 的 corpus cache 取得），
# 驗證雜湊後就地使用，不複製也不另外下載。
if ($SourceFontPath) {
    if (-not (Test-Path -LiteralPath $SourceFontPath)) {
        throw "指定的來源字型不存在：$SourceFontPath"
    }

    $suppliedHash = (Get-FileHash -LiteralPath $SourceFontPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($suppliedHash -ne $definition.sha256) {
        throw "提供的來源字型 SHA-256 不符合鎖定值：$SourceFontPath"
    }

    $fontPath = [IO.Path]::GetFullPath($SourceFontPath)
    Write-Host "就地複用既有鎖定字型：$fontPath"
}

# 與其它 WebFont 閘門相同：先比對既有檔案雜湊，僅在缺漏或不符時才重新下載。
$needsDownload = -not $SourceFontPath
if (-not $SourceFontPath -and (Test-Path -LiteralPath $fontPath)) {
    $existingHash = (Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($existingHash -eq $definition.sha256) { $needsDownload = $false }
}

if ($needsDownload) {
    Write-Host "下載鎖定來源字型：$($definition.uri)"
    $temporaryPath = "$fontPath.download"
    Invoke-WebRequest -Uri $definition.uri -OutFile $temporaryPath -MaximumRetryCount 3 -RetryIntervalSec 5
    $actualHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $definition.sha256) {
        Remove-Item -LiteralPath $temporaryPath -Force
        throw "下載檔 SHA-256 不符合：$($definition.uri)"
    }
    Move-Item -LiteralPath $temporaryPath -Destination $fontPath -Force
}

Write-Host "來源字型：$($definition.fileName) $($definition.version)（$($definition.license)）"

$project = Join-Path $repoRoot "tests" "OdfKit.WebFontCmapScaleProof" "OdfKit.WebFontCmapScaleProof.csproj"
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "無法建置 cmap 規模證明 harness。" }

# Playwright 瀏覽器需存在；CI 上由 playwright.ps1 安裝。
$playwright = Join-Path $repoRoot "tests" "OdfKit.WebFontCmapScaleProof" "bin" "Release" "net10.0" "playwright.ps1"
if (Test-Path -LiteralPath $playwright) {
    & $playwright install chromium firefox webkit
    if ($LASTEXITCODE -ne 0) { throw "無法安裝 Playwright 瀏覽器。" }
}

dotnet run --project $project -c Release --no-build -- $destinationPath $fontPath
if ($LASTEXITCODE -ne 0) { throw "cmap 規模瀏覽器證明失敗。" }

Write-Host "PASS：cmap format 4 規模路徑已於 Chromium／Firefox／WebKit 取得實機證據。"
