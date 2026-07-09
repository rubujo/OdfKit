#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$sourcePath = Join-Path $textDir 'TextDocument.TrackChanges.Recording.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.Collections.Generic')
    [void]$needed.Add('System.Globalization')
    [void]$needed.Add('System.Text')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')

    $order = @('System', 'System.Collections.Generic', 'System.Globalization', 'System.Text', 'OdfKit.Core', 'OdfKit.DOM')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($usingLine in (Get-UsingsForBlock -Text ($BodyLines -join "`n"))) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Text;')
    $out.Add('')
    $out.Add('public partial class TextDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$extractionBody = $lines[213..259] + $lines[340..434]
Write-PartialFile -Path (Join-Path $textDir 'TextDocument.TrackChanges.TextExtraction.cs') -RegionName 'Tracked Changes - Text Extraction' -BodyLines $extractionBody
Write-Host "  TextDocument.TrackChanges.TextExtraction.cs: $($extractionBody.Count) body lines"

$recordingBody = $lines[13..211] + $lines[262..338]
$out = [System.Collections.Generic.List[string]]::new()
foreach ($usingLine in (Get-UsingsForBlock -Text ($recordingBody -join "`n"))) { $out.Add($usingLine) }
$out.Add('')
$out.Add('namespace OdfKit.Text;')
$out.Add('')
$out.Add('public partial class TextDocument')
$out.Add('{')
$out.Add('    #region Tracked Changes - Recording')
$out.Add('')
foreach ($line in $recordingBody) { $out.Add($line) }
$out.Add('')
$out.Add('    #endregion')
$out.Add('}')
Set-Content -Path $sourcePath -Value $out -Encoding UTF8
Write-Host "Core TextDocument.TrackChanges.Recording.cs: $($out.Count) lines"
Write-Host 'Done.'