#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$stylesDir = Join-Path $PSScriptRoot '..\OdfKit\Styles'
$sourcePath = Join-Path $stylesDir 'OdfNumberFormatter.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$helperBlocks = @(
    @{ Start = 10; End = 46; File = 'FormatType.cs' }
    @{ Start = 47; End = 62; File = 'DateTimeToken.cs' }
    @{ Start = 63; End = 98; File = 'FormatInfo.cs' }
)

$regionMap = [ordered]@{
    '內部解析與翻譯邏輯' = 'OdfNumberFormatter.Parsing.cs'
    'DOM 操作與快取去重'   = 'OdfNumberFormatter.DomCache.cs'
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo|NumberFormatInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }

    $order = @('System', 'System.Collections.Generic', 'System.Globalization', 'System.Text', 'OdfKit.Core', 'OdfKit.DOM')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-HelperFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Styles;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Styles;')
    $out.Add('')
    $out.Add('public partial class OdfNumberFormatter')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $helperBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-HelperFile -Path (Join-Path $stylesDir $block.File) -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

$classLines = $lines[99..($lines.Count - 1)]
$coreEnd = 0
$lastRegionEnd = 0
for ($i = 0; $i -lt $classLines.Count; $i++) {
    if ($classLines[$i] -match '^\s*#region ' -and $coreEnd -eq 0) { $coreEnd = $i - 1 }
    if ($classLines[$i] -match '^\s*#endregion') { $lastRegionEnd = $i }
}

$header = $lines[0..7]
$coreBody = $classLines[0..$coreEnd] | ForEach-Object {
    $_ -replace '^public class OdfNumberFormatter', 'public partial class OdfNumberFormatter'
}
$core = $header + $coreBody
if ($lastRegionEnd -gt 0 -and $lastRegionEnd + 1 -lt $classLines.Count) {
    $core += $classLines[($lastRegionEnd + 1)..($classLines.Count - 1)]
}
elseif ($core[-1] -notmatch '^\s*}\s*$') {
    $core += '}'
}
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfNumberFormatter.cs: $($core.Count) lines"

$regionBody = $classLines[($coreEnd + 1)..$lastRegionEnd]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-Region {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $regionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown region: $($script:currentRegion)" }
    Write-PartialFile -Path (Join-Path $stylesDir $fileName) -RegionName $script:currentRegion -BodyLines $script:currentLines
    Write-Host "  $fileName : $($script:currentLines.Count) body lines"
    $script:currentLines.Clear()
}

foreach ($line in $regionBody) {
    if ($line -match '^\s*#region\s+(.+)$') {
        Flush-Region
        $currentRegion = $Matches[1].Trim()
        continue
    }
    if ($line -match '^\s*#endregion') {
        Flush-Region
        $currentRegion = $null
        continue
    }
    if ($null -ne $currentRegion) { $currentLines.Add($line) }
}
Flush-Region
Write-Host 'Done.'