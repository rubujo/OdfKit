#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$sourcePath = Join-Path $textDir 'OdfIndexes.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$lineCount = $lines.Count
$typeBlocks = @(
    @{ Start = 10; End = 102; File = 'OdfIndex.cs'; IsCore = $true }
    @{ Start = 103; End = 295; File = 'OdfTableOfContents.cs' }
    @{ Start = 296; End = 527; File = 'OdfAlphabeticalIndex.cs' }
    @{ Start = 528; End = 551; File = 'OdfIndexMarkInfo.cs' }
    @{ Start = 552; End = 608; File = 'OdfIndexTemplateBuilder.cs' }
    @{ Start = 609; End = 775; File = 'OdfBibliography.cs' }
    @{ Start = 776; End = 799; File = 'OdfBibliographyMarkInfo.cs' }
    @{ Start = 800; End = 834; File = 'OdfBibliographyTemplateBuilder.cs' }
    @{ Start = 835; End = 873; File = 'OdfAlphabeticalIndexMark.cs' }
    @{ Start = 874; End = $lineCount; File = 'OdfBibliographyMark.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Enumerable\.|\.Linq|\.OrderBy\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Linq', 'System.Text',
        'OdfKit.Core', 'OdfKit.DOM'
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
        foreach ($line in $lines[0..7]) { $out.Add($line) }
        $out.Add('')
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.Text;')
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $textDir $block.File
    if ($block.IsCore) {
        Write-TypeFile -Path $path -BodyLines $body -IsCore
        Write-Host "Core $($block.File): $($body.Count) body lines"
        Remove-Item -Path $sourcePath -Force
    }
    else {
        Write-TypeFile -Path $path -BodyLines $body
        Write-Host "  $($block.File): $($body.Count) body lines"
    }
}

Write-Host 'Done.'