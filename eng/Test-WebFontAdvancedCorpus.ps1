#Requires -Version 7.0
<#
.SYNOPSIS
驗證外部 AAT、Graphite、variable 與 color 真實字型 corpus 的來源與能力邊界。
.DESCRIPTION
讀取 corpus-root/manifest.json，核對每個字型的 SHA-256 與必要 sfnt table，並執行
managed engine 的相關拒絕及格式測試。此閘門不會把 AAT／Graphite 描述為已支援。
#>
[CmdletBinding()]
param(
    [string]$CorpusRoot = $env:ODFKIT_WEBFONT_ADVANCED_CORPUS_ROOT,
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($CorpusRoot)) {
    Write-Host "ODFKIT_WEBFONT_ADVANCED_CORPUS_ROOT is not set; skipping advanced font corpus validation."
    exit 0
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$root = [IO.Path]::GetFullPath($CorpusRoot)
$manifestPath = Join-Path $root "manifest.json"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "找不到進階字型 corpus manifest：$manifestPath"
}

function Read-U16BE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or $Offset + 2 -gt $Bytes.Length) { throw "sfnt u16 超出範圍。" }
    return ([int]$Bytes[$Offset] -shl 8) -bor [int]$Bytes[$Offset + 1]
}

function Read-U32BE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or $Offset + 4 -gt $Bytes.Length) { throw "sfnt u32 超出範圍。" }
    return ([uint32]$Bytes[$Offset] -shl 24) -bor
        ([uint32]$Bytes[$Offset + 1] -shl 16) -bor
        ([uint32]$Bytes[$Offset + 2] -shl 8) -bor
        [uint32]$Bytes[$Offset + 3]
}

function Get-SfntTags([string]$Path) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 12) { throw "字型過短：$Path" }
    $faceOffset = 0
    if ([Text.Encoding]::ASCII.GetString($bytes, 0, 4) -eq "ttcf") {
        $faceOffset = [int](Read-U32BE $bytes 12)
    }
    $count = Read-U16BE $bytes ($faceOffset + 4)
    if ($count -le 0 -or $count -gt 256 -or $faceOffset + 12 + ($count * 16) -gt $bytes.Length) {
        throw "sfnt table directory 無效：$Path"
    }
    $tags = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($index = 0; $index -lt $count; $index++) {
        $tag = [Text.Encoding]::ASCII.GetString($bytes, $faceOffset + 12 + ($index * 16), 4)
        [void]$tags.Add($tag)
    }
    return $tags
}

$requirements = @{
    aat = @("morx", "mort", "kerx", "ankr", "trak")
    graphite = @("Silf", "Glat", "Gloc", "Feat", "Sill")
    variable = @("fvar", "gvar", "CFF2")
    color = @("COLR", "CBDT", "SVG ", "sbix")
}
$counts = @{ aat = 0; graphite = 0; variable = 0; color = 0 }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($font in @($manifest.fonts)) {
    $category = [string]$font.category
    if (-not $requirements.ContainsKey($category)) { throw "未知 corpus category：$category" }
    $path = [IO.Path]::GetFullPath((Join-Path $root ([string]$font.path)))
    $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Corpus 字型路徑超出根目錄：$path"
    }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne ([string]$font.sha256).ToLowerInvariant()) {
        throw "Corpus SHA-256 不符：$path"
    }
    $tags = Get-SfntTags $path
    if (-not @($requirements[$category] | Where-Object { $tags.Contains($_) }).Count) {
        throw "Corpus 字型缺少 $category 必要 table：$path"
    }
    $counts[$category]++
}

foreach ($category in $requirements.Keys) {
    if ($counts[$category] -le 0) { throw "Corpus 缺少 $category 真實字型。" }
}

$project = Join-Path $repoRoot "tests/OdfKit.WebFonts.Tests/OdfKit.WebFonts.Tests.csproj"
$arguments = @(
    "test", $project, "-c", $Configuration, "--framework", $Framework, "--no-restore",
    "--filter", "FullyQualifiedName~SfntFontTests|FullyQualifiedName~GvarSubsetterTests|FullyQualifiedName~ColorFontValidatorTests|FullyQualifiedName~Cff2SubsetterTests"
)
if ($NoBuild) { $arguments += "--no-build" }
dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "進階字型能力測試失敗。" }
Write-Host "進階字型 corpus 通過：AAT=$($counts.aat)、Graphite=$($counts.graphite)、variable=$($counts.variable)、color=$($counts.color)。"
