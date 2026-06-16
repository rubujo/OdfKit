#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$presDir = Join-Path $PSScriptRoot '..\OdfKit\Presentation'
$sourcePath = Join-Path $presDir 'PresentationAnimations.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 8; End = 28; File = 'OdfAnimationNodeType.cs' }
    @{ Start = 29; End = 219; File = 'OdfAnimationNode.cs' }
    @{ Start = 220; End = 297; File = 'PresentationAnimationEnums.cs' }
    @{ Start = 298; End = $lineCount; File = 'OdfAnimation.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'OdfLength|OdfStyle') { [void]$needed.Add('OdfKit.Styles') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.DOM', 'OdfKit.Styles')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Presentation;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $presDir $block.File
    Write-TypeFile -Path $path -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
    if ($block.File -eq 'OdfAnimation.cs') {
        Remove-Item -Path $sourcePath -Force
    }
}

Write-Host 'Done.'