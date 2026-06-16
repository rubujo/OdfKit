#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfPresentationTokens.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 3; End = 92; File = 'OdfPresentationEffect.cs' }
    @{ Start = 94; End = 113; File = 'OdfPresentationSpeed.cs' }
    @{ Start = 115; End = 184; File = 'OdfPresentationAction.cs' }
    @{ Start = 186; End = 205; File = 'OdfPresentationTransitionType.cs' }
    @{ Start = 207; End = $lineCount; File = 'OdfPresentationTransitionStyle.cs' }
)

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-TypeFile -Path (Join-Path $domDir $block.File) -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed OdfPresentationTokens.cs'
Write-Host 'Done.'