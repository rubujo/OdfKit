#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfElement.EnumParsers.TextNumberingAndKind.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 12; End = 244; File = 'OdfElement.EnumParsers.TextNumberingAndAnimation.cs'; Region = 'Enum Parsers - Text Numbering & Animation' }
    @{ Start = 246; End = 460; File = 'OdfElement.EnumParsers.TextKindStyleFormAndTable.cs'; Region = 'Enum Parsers - Text Kind, Style, Form & Table' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'OdfPackage|OdfNode|OdfNamespaces|OdfNodeFactory') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfText|OdfStyle|OdfForm|OdfTable') { [void]$needed.Add('OdfKit.Styles') }

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

function Write-CloneFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $out.Add('using OdfKit.Core;')
    $out.Add('')
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add('public partial class OdfElement')
    $out.Add('{')
    $out.Add('    #region Clone')
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

$cloneBody = $lines[461..481]
Write-CloneFile -Path (Join-Path $domDir 'OdfElement.Clone.cs') -BodyLines $cloneBody
Write-Host "  OdfElement.Clone.cs: $($cloneBody.Count) body lines"

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed OdfElement.EnumParsers.TextNumberingAndKind.cs'
Write-Host 'Done.'