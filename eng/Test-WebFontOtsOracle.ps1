#requires -Version 7.0
<#
.SYNOPSIS
    以 OpenType Sanitiser 對 OdfKit 產出的 WebFont 資產做差分驗證。

.DESCRIPTION
    OTS 是 Chromium 與 Firefox 內建的字型消毒器：瀏覽器不會把 @font-face 下載的
    位元組直接交給作業系統字型引擎，而是先經 OTS 解析、驗證並重新序列化，任一步
    失敗即整個拒絕。因此「通過 OTS」是「能在瀏覽器載入」的必要條件。

    OdfKit 的子集 writer 是 clean-room 實作，刻意不參考 FontTools／HarfBuzz／
    FreeType 原始碼。這對授權有利，但代價是規格讀錯的地方沒有任何東西會指出來
    ——本專案先前即因此累積了 cmap encoding record 未依規格排序、format 4 長度
    上限等問題，而既有的 mutation 測試、瀏覽器截圖與 managed verifier 都沒有攔下。
    差分預言機補的正是這個缺口：拿一個獨立實作來反對我們的判斷。

    關鍵方向是「OdfKit 接受但 OTS 拒絕」——那代表產出的資產在瀏覽器裡會靜默不
    載入。腳本對每個產出資產執行 OTS，任何一個遭拒即失敗。

    另含負向對照：刻意截斷的資產必須被 OTS 拒絕。若對照通過，代表這個預言機根本
    沒有在判別，正向結果也不可採信。

    OTS 只作隔離 oracle，不進入任何產品相依圖（見 docs/webfont-managed-architecture.md
    第 6 節）。
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot ".." "artifacts" "webfont-ots-oracle"),

    # 既有 WebFont 閘門已下載並快取同一支鎖定字型；指向該檔即可避免重複下載。
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
$otsDefinition = $manifest.opentypeSanitizer
$fontDefinition = $manifest.webFontSmoke.internationalFonts.cjkOpenType

$sourceRoot = Join-Path $destinationPath "sources"
$toolRoot = Join-Path $destinationPath "tools"
$assetRoot = Join-Path $destinationPath "assets"
$evidenceRoot = Join-Path $destinationPath "evidence"
foreach ($path in @($sourceRoot, $toolRoot, $assetRoot, $evidenceRoot)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

# ---------------------------------------------------------------- OTS 佈建
if (-not $IsWindows) {
    throw "目前僅釘選 Windows wheel；其它平台需另行釘選對應 wheel 的 SHA-256。"
}

$wheelDefinition = $otsDefinition.windowsWheel
$otsExecutable = Join-Path $toolRoot $wheelDefinition.executablePath
if (-not (Test-Path -LiteralPath $otsExecutable)) {
    $wheelDirectory = Join-Path $toolRoot "wheel"
    New-Item -ItemType Directory -Path $wheelDirectory -Force | Out-Null
    Write-Host "取得 OpenType Sanitiser $($otsDefinition.version)…"

    # 只下載不安裝：避免更動使用者或 runner 的 Python 環境。
    & python -m pip download "opentype-sanitizer==$($otsDefinition.version)" `
        --dest $wheelDirectory --no-deps --quiet
    if ($LASTEXITCODE -ne 0) { throw "無法取得 opentype-sanitizer wheel。" }

    $wheel = Join-Path $wheelDirectory $wheelDefinition.fileName
    if (-not (Test-Path -LiteralPath $wheel)) {
        throw "下載結果不含預期的 wheel：$($wheelDefinition.fileName)"
    }

    $actualHash = (Get-FileHash -LiteralPath $wheel -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $wheelDefinition.sha256) {
        throw "opentype-sanitizer wheel SHA-256 不符合鎖定值：$actualHash"
    }

    Expand-Archive -LiteralPath $wheel -DestinationPath $toolRoot -Force
}

if (-not (Test-Path -LiteralPath $otsExecutable)) {
    throw "解壓後找不到 ots-sanitize：$otsExecutable"
}

$reportedVersion = (& $otsExecutable --version 2>&1 | Select-Object -First 1)
Write-Host "OTS：$reportedVersion"

# ---------------------------------------------------------------- 來源字型
$fontPath = Join-Path $sourceRoot $fontDefinition.fileName
if ($SourceFontPath) {
    if (-not (Test-Path -LiteralPath $SourceFontPath)) {
        throw "指定的來源字型不存在：$SourceFontPath"
    }

    $suppliedHash = (Get-FileHash -LiteralPath $SourceFontPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($suppliedHash -ne $fontDefinition.sha256) {
        throw "提供的來源字型 SHA-256 不符合鎖定值：$SourceFontPath"
    }

    $fontPath = [IO.Path]::GetFullPath($SourceFontPath)
    Write-Host "就地複用既有鎖定字型：$fontPath"
}
elseif (-not (Test-Path -LiteralPath $fontPath)) {
    Write-Host "下載鎖定來源字型：$($fontDefinition.uri)"
    Invoke-WebRequest -Uri $fontDefinition.uri -OutFile $fontPath -MaximumRetryCount 3 -RetryIntervalSec 5
    $actualHash = (Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $fontDefinition.sha256) {
        throw "下載檔 SHA-256 不符合：$($fontDefinition.uri)"
    }
}

# ---------------------------------------------------------------- 產生資產
$cliProject = Join-Path $repoRoot "OdfKit.WebFonts.Build" "OdfKit.WebFonts.Build.csproj"
dotnet build $cliProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "無法建置 WebFont CLI。" }

# 涵蓋不同字集規模：小樣本、跨越 cmap format 4 合併路徑，以及超過其長度上限而
# 必須省略 format 4 的稀疏字集。三者的 cmap 結構不同，OTS 的檢查路徑也不同。
$corpusCases = @(
    @{ Name = "small"; Text = "漢字測試" }
    @{ Name = "dense"; Text = -join ([char[]](0x4E00..0x5FFF)) }
    @{ Name = "sparse"; Text = -join (0..8999 | ForEach-Object { [char](0x4E00 + ($_ * 2)) }) }
)

$results = [System.Collections.Generic.List[object]]::new()
$failures = 0

foreach ($case in $corpusCases) {
    $corpusPath = Join-Path $assetRoot "$($case.Name).txt"
    Set-Content -LiteralPath $corpusPath -Value $case.Text -Encoding utf8NoBOM
    $caseOutput = Join-Path $assetRoot $case.Name

    dotnet run --project $cliProject -c Release --no-build -- build `
        --font $fontPath `
        --text $corpusPath `
        --output $caseOutput `
        --family "OtsOracle" `
        --profile "ots-oracle-v1" `
        --font-id "oracle" `
        --formats "otf,woff,woff2" `
        --max-scalars 200000 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "產生 $($case.Name) 資產失敗。" }

    foreach ($asset in Get-ChildItem -LiteralPath $caseOutput -Recurse -File -Include *.otf, *.woff, *.woff2) {
        $sanitized = Join-Path $evidenceRoot "sanitized.tmp"
        $output = & $otsExecutable $asset.FullName $sanitized 2>&1 | Out-String
        $accepted = $LASTEXITCODE -eq 0
        if (-not $accepted) { $failures++ }

        $results.Add([ordered]@{
            case = $case.Name
            asset = $asset.Name
            format = $asset.Extension.TrimStart('.')
            bytes = $asset.Length
            otsAccepted = $accepted
            otsOutput = $output.Trim()
            expectedAccepted = $true
        })

        $status = if ($accepted) { "PASS" } else { "FAIL" }
        Write-Host ("[{0}/{1}] {2} bytes -> OTS {3}" -f $case.Name, $asset.Extension.TrimStart('.'), $asset.Length, $status)
    }
}

# ---------------------------------------------------------------- 剪枝回歸
# OTS 與瀏覽器只驗證正確性，不驗證 subroutine 剪枝是否真的發生：若剪枝失效
# （例如誤用未帶 usage 的多載），輸出仍是合法字型，兩個閘門都不會紅。因此另以
# 體積上界固定此行為。剪枝前 small/woff2 為 1,166,964 bytes，剪枝後 281,708；
# 上界取 500,000 以容納字型或壓縮器的正常變動，同時能明確攔下剪枝失效。
$smallWoff2 = Get-ChildItem -LiteralPath (Join-Path $assetRoot "small") -Recurse -File -Include *.woff2 |
    Select-Object -First 1
$pruningLimit = 500000
$pruningHeld = $smallWoff2.Length -lt $pruningLimit
if (-not $pruningHeld) { $failures++ }
$results.Add([ordered]@{
    case = "subroutine-pruning"
    asset = $smallWoff2.Name
    format = "woff2"
    bytes = $smallWoff2.Length
    limitBytes = $pruningLimit
    otsAccepted = $true
    expectedAccepted = $true
})
Write-Host ("[pruning] small/woff2 {0:N0} bytes（上界 {1:N0}）-> {2}" -f `
    $smallWoff2.Length, $pruningLimit, $(if ($pruningHeld) { "PASS" } else { "FAIL：剪枝可能已失效" }))

# ---------------------------------------------------------------- 負向對照
# 若截斷的資產仍被接受，代表這個預言機沒有在判別，上方所有 PASS 都不可採信。
$controlSource = Get-ChildItem -LiteralPath $assetRoot -Recurse -File -Include *.woff2 |
    Select-Object -First 1
$controlPath = Join-Path $evidenceRoot "control-truncated.woff2"
$controlBytes = [IO.File]::ReadAllBytes($controlSource.FullName)
[IO.File]::WriteAllBytes($controlPath, $controlBytes[0..([int]($controlBytes.Length * 0.6))])

$controlOutput = & $otsExecutable $controlPath (Join-Path $evidenceRoot "control.tmp") 2>&1 | Out-String
$controlAccepted = $LASTEXITCODE -eq 0
if ($controlAccepted) { $failures++ }

$results.Add([ordered]@{
    case = "control"
    asset = "control-truncated.woff2"
    format = "woff2"
    bytes = (Get-Item -LiteralPath $controlPath).Length
    otsAccepted = $controlAccepted
    otsOutput = $controlOutput.Trim()
    expectedAccepted = $false
})
Write-Host ("[control] 截斷資產 -> OTS {0}（預期拒絕）" -f $(if ($controlAccepted) { "接受：FAIL" } else { "拒絕：PASS" }))

# ---------------------------------------------------------------- 證據
$evidencePath = Join-Path $evidenceRoot "ots-oracle.json"
[ordered]@{
    otsVersion = $otsDefinition.version
    otsLicense = $otsDefinition.license
    sourceFont = $fontDefinition.fileName
    sourceFontSha256 = $fontDefinition.sha256
    results = $results
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8NoBOM
Write-Host "證據已寫出：$evidencePath"

Remove-Item -LiteralPath (Join-Path $evidenceRoot "sanitized.tmp") -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $evidenceRoot "control.tmp") -ErrorAction SilentlyContinue

if ($failures -ne 0) {
    throw "OTS 差分驗證失敗：$failures 項不符預期。"
}

Write-Host "PASS：所有產出資產均通過 OTS，且負向對照正確遭拒。"
