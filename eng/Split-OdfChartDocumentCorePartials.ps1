#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$chartDir = Join-Path $PSScriptRoot '..\OdfKit\Chart'
$sourcePath = Join-Path $chartDir 'OdfChartDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$blocks = @(
    @{ Start = 203; End = 417; File = 'OdfChartDocument.DataRange.cs'; Region = 'Data Range Binding' }
    @{ Start = 419; End = 651; File = 'OdfChartDocument.SeriesAxis.cs'; Region = 'Series & Axis' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Stream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'OdfCellRange|OdfCellAddress|Spreadsheet') { [void]$needed.Add('OdfKit.Spreadsheet') }
    if ($Text -match 'OdfCompliance|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }

    $order = @('System', 'System.Collections.Generic', 'System.IO', 'System.Text', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Spreadsheet')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Chart;')
    $out.Add('')
    $out.Add('public partial class OdfChartDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreParts = $lines[0..200] + $lines[652..($lineCount - 3)]
$coreParts[15] = $coreParts[15] -replace '^public class OdfChartDocument', 'public partial class OdfChartDocument'
$coreParts += '}'
Set-Content -Path $sourcePath -Value $coreParts -Encoding UTF8
Write-Host "Core OdfChartDocument.cs: $($coreParts.Count) lines"

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $chartDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'