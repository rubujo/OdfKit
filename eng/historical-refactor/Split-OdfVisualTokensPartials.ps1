#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfVisualTokens.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 3; End = 280; File = 'OdfVisualTokens.Layout.cs'; Region = 'Visual Tokens - Layout' }
    @{ Start = 282; End = $lineCount; File = 'OdfVisualTokens.Presentation.cs'; Region = 'Visual Tokens - Presentation & Stroke' }
)

function Write-EnumFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add("#region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('#endregion')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-EnumFile -Path (Join-Path $domDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed OdfVisualTokens.cs'
Write-Host 'Done.'