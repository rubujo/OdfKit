#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfElement.EnumParsers.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionStarts = @(12, 644, 1138, 1663)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'OdfPackage|OdfNode|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine') { [void]$needed.Add('OdfKit.Styles') }

    $order = @('System', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param(
        [string]$Path,
        [string]$RegionName,
        [string[]]$BodyLines
    )
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
    $out.AddRange($BodyLines)
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$files = @(
    @{ Start = 12; File = 'OdfElement.EnumParsers.A.cs'; Region = 'Enum Parsers (A)' }
    @{ Start = 644; File = 'OdfElement.EnumParsers.B.cs'; Region = 'Enum Parsers (B)' }
    @{ Start = 1138; File = 'OdfElement.EnumParsers.C.cs'; Region = 'Enum Parsers (C)' }
    @{ Start = 1663; File = 'OdfElement.EnumParsers.D.cs'; Region = 'Enum Parsers (D)' }
)
for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($i + 1 -lt $files.Count) { $files[$i + 1].Start - 2 } else { $lines.Count - 3 }
    $block = $lines[$start..$end]
    Write-PartialFile -Path (Join-Path $domDir $files[$i].File) -RegionName $files[$i].Region -BodyLines $block
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host "Removed $sourcePath"
Write-Host 'Done.'