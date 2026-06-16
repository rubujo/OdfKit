#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$sourcePath = Join-Path $textDir 'OdfParagraph.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$blocks = @(
    @{ Start = 294; End = 372; File = 'OdfParagraph.Fields.cs'; Region = 'Fields & References' }
    @{ Start = 374; End = 468; File = 'OdfParagraph.EmbeddedContent.cs'; Region = 'Embedded Content & Layout' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    [void]$needed.Add('OdfKit.Styles')
    if ($Text -match 'List<|Dictionary<|HashSet<|IEnumerable<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'IEnumerator|IEnumerable') { [void]$needed.Add('System.Collections') }

    $order = @('System', 'System.Collections', 'System.Collections.Generic', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
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
    $out.Add('public partial class OdfParagraph')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$core = $lines[0..291] + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfParagraph.cs: $($core.Count) lines"

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $textDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'