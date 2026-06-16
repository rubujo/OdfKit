#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$drawDir = Join-Path $PSScriptRoot '..\OdfKit\Drawing'
$sourcePath = Join-Path $drawDir 'DrawingDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$lineCount = $lines.Count
$typeBlocks = @(
    @{ Start = 15; End = 297; File = 'DrawingDocument.cs'; IsCore = $true }
    @{ Start = 298; End = 350; File = 'OdfDrawPageCollection.cs' }
    @{ Start = 351; End = 776; File = 'OdfDrawPage.cs' }
    @{ Start = 777; End = 857; File = 'OdfDrawGroup.cs' }
    @{ Start = 858; End = $lineCount; File = 'OdfConnectorType.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'IEnumerable|IReadOnlyList|IEnumerator|ICollection|IEnumerable') { [void]$needed.Add('System.Collections') }
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Enumerable\.|\.Linq') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'MemoryStream|Stream|File\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'OdfCompliance|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfShape|OdfSlide') { [void]$needed.Add('OdfKit.Presentation') }
    if ($Text -match 'OdfLength|OdfStyle') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections', 'System.Collections.Generic', 'System.IO', 'System.Linq',
        'System.Text', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Presentation', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-TypeFile {
    param(
        [string]$Path,
        [string[]]$BodyLines,
        [switch]$IsCore
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if ($IsCore) {
        foreach ($line in $lines[0..12]) { $out.Add($line) }
        $out.Add('')
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.Drawing;')
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $drawDir $block.File
    if ($block.IsCore) {
        Write-TypeFile -Path $path -BodyLines $body -IsCore
        Write-Host "Core $($block.File): $($body.Count) body lines"
    }
    else {
        Write-TypeFile -Path $path -BodyLines $body
        Write-Host "  $($block.File): $($body.Count) body lines"
    }
}

Write-Host 'Done.'