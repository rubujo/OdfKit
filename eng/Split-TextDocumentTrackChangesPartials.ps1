#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$sourcePath = Join-Path $textDir 'TextDocument.TrackChanges.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$blocks = @(
    @{ Start = 13; End = 434; File = 'TextDocument.TrackChanges.Recording.cs'; Region = 'Tracked Changes - Recording' }
    @{ Start = 436; End = 655; File = 'TextDocument.TrackChanges.AcceptReject.cs'; Region = 'Tracked Changes - Accept/Reject' }
    @{ Start = 657; End = ($lineCount - 2); File = 'TextDocument.TrackChanges.Helpers.cs'; Region = 'Tracked Changes - Helpers' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IEnumerable<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'StringBuilder') { [void]$needed.Add('System.Text') }
    if ($Text -match 'CultureInfo|DateTimeStyles') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'OdfNode|OdfPackage|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }

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
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
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

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $textDir $block.File
    Write-PartialFile -Path $path -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Done.'