#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$presDir = Join-Path $PSScriptRoot '..\OdfKit\Presentation'
$sourcePath = Join-Path $presDir 'PresentationDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$typeBlocks = @(
    @{ Start = 14; End = 106; File = 'PresentationDocument.Enums.cs'; SkipWrapper = $true }
    @{ Start = 108; End = 622; File = 'PresentationDocument.cs'; SkipWrapper = $true; IsCore = $true }
    @{ Start = 624; End = 675; File = 'OdfSlideCollection.cs' }
    @{ Start = 677; End = 1203; File = 'OdfSlide.cs' }
    @{ Start = 1205; End = 1436; File = 'OdfShape.cs' }
    @{ Start = 1438; End = 1454; File = 'OdfMediaObject.cs' }
    @{ Start = 1456; End = 1497; File = 'OdfEmbeddedTable.cs' }
    @{ Start = 1499; End = 1546; File = 'OdfTextBox.cs' }
    @{ Start = 1548; End = 1595; File = 'OdfPicture.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'IEnumerable|IReadOnlyList|IEnumerator|ICollection') { [void]$needed.Add('System.Collections') }
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Enumerable\.|\.Linq') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'MemoryStream|Stream|File\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'OdfCompliance|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfLength|OdfStyle') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections', 'System.Collections.Generic', 'System.IO', 'System.Linq',
        'System.Text', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles'
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
        [switch]$SkipWrapper,
        [switch]$IsCore
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if ($IsCore) {
        foreach ($line in $lines[0..11]) { $out.Add($line) }
        $out.Add('')
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.Presentation;')
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $presDir $block.File
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