#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$sheetDir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$sourcePath = Join-Path $sheetDir 'OdfCell.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$blocks = @(
    @{ Start = 315; End = 531; File = 'OdfCell.Hyperlink.cs'; Region = 'Hyperlink, Rich Text & Annotation' }
    @{ Start = 533; End = ($lineCount - 1); File = 'OdfCell.Formatting.cs'; Region = 'Borders, Conditional Format & Display' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Color\.|System\.Drawing|OdfColor') { [void]$needed.Add('System.Drawing') }
    if ($Text -match 'CultureInfo|NumberStyles') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'OdfBorder|OdfLength|OdfStyle|StyleEngine|OdfNumberFormatEngine') { [void]$needed.Add('OdfKit.Styles') }

    $order = @('System', 'System.Collections.Generic', 'System.Drawing', 'System.Globalization', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
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
    $out.Add('namespace OdfKit.Spreadsheet;')
    $out.Add('')
    $out.Add('public partial class OdfCell')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$core = $lines[0..313]
$core[20] = $core[20] -replace '^public class OdfCell', 'public partial class OdfCell'
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfCell.cs: $($core.Count) lines"

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $sheetDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'