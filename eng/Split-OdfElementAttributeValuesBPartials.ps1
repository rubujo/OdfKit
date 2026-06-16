#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfElement.AttributeValues.DrawGeometryTextAndLine.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionStarts = @(13, 825, 1188)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfNode|OdfNamespaces|OdfVersionInfo') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine') { [void]$needed.Add('OdfKit.Styles') }

    $order = @('System', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param(
        [string]$Path,
        [string]$RegionName,
        [string[]]$BodyLines
    )
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add('public partial class OdfElement')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    $out.AddRange($BodyLines)
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$files = @(
    @{ Start = 13; File = 'OdfElement.AttributeValues.DrawFoAndTextList.cs'; Region = 'Attribute Values - Draw, FO & Text List' }
    @{ Start = 825; File = 'OdfElement.AttributeValues.GeometryLocaleAndLine.cs'; Region = 'Attribute Values - Geometry, Locale & Line' }
    @{ Start = 1188; File = 'OdfElement.AttributeValues.LineFontStyleAndMedia.cs'; Region = 'Attribute Values - Line, Font, Style & Media' }
)
for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($i + 1 -lt $files.Count) { $files[$i + 1].Start - 2 } else { $lines.Count - 3 }
    $block = $lines[$start..$end]
    Write-PartialFile -Path (Join-Path $domDir $files[$i].File) -RegionName $files[$i].Region -BodyLines $block
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host "Removed $sourcePath"
Write-Host 'Done.'