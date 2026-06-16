#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$presDir = Join-Path $PSScriptRoot '..\OdfKit\Presentation'
$sourcePath = Join-Path $presDir 'OdfSlide.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 224; End = ($lineCount - 1); File = 'OdfSlide.DrawingObjects.cs'; Region = 'Slide Drawing Objects' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    [void]$needed.Add('OdfKit.Styles')
    if ($Text -match 'List<|Dictionary<|HashSet<|IEnumerable<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match '\.Linq|\.Select\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }

    $order = @('System', 'System.Collections.Generic', 'System.Globalization', 'System.Linq', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
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
    $out.Add('namespace OdfKit.Presentation;')
    $out.Add('')
    $out.Add('public partial class OdfSlide')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = 222
$core = $lines[0..($coreEnd - 1)] + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfSlide.cs: $($core.Count) lines"

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $presDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'