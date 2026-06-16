#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfElement.EnumParsers.PresentationEffectsAndTransitions.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 10; End = 224; File = 'OdfElement.EnumParsers.PresentationEffectAndTransition.cs'; Region = 'Enum Parsers - Presentation Effect & Transition' }
    @{ Start = 226; End = ($lineCount - 3); File = 'OdfElement.EnumParsers.PresentationTransitionStyles.cs'; Region = 'Enum Parsers - Presentation Transition Styles' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'OdfPackage|OdfNode|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfPresentation|OdfStyle|OdfAnimation') { [void]$needed.Add('OdfKit.Styles') }

    $order = @('System', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
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
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add('public partial class OdfElement')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $domDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed OdfElement.EnumParsers.PresentationEffectsAndTransitions.cs'
Write-Host 'Done.'