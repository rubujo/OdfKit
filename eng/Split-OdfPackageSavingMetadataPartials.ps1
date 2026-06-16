#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfPackage.Saving.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'Task\.|CancellationToken') {
        [void]$needed.Add('System.Threading')
        [void]$needed.Add('System.Threading.Tasks')
    }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XmlReader|XmlWriter|XmlException|XmlDocument') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'XElement|XDocument') { [void]$needed.Add('System.Xml.Linq') }
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'DefaultFormulaEvaluator|IOdfFormulaEvaluator') { [void]$needed.Add('OdfKit.Formula') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine|OdfFontResolver') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.IO',
        'System.Text', 'System.Threading', 'System.Threading.Tasks', 'System.Xml', 'System.Xml.Linq',
        'OdfKit.Compliance', 'OdfKit.DOM', 'OdfKit.Formula', 'OdfKit.Styles'
    )
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
    $out.Add('namespace OdfKit.Core;')
    $out.Add('')
    $out.Add('public sealed partial class OdfPackage')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$regionLine = 22
$metadataStart = 332
$coreBody = $lines[($regionLine + 1)..($metadataStart - 2)]
$core = $lines[0..$regionLine] + $coreBody + @('    #endregion', '}')
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfPackage.Saving.cs: $($core.Count) lines"

$metadataBody = $lines[($metadataStart - 1)..($lineCount - 3)]
Write-PartialFile -Path (Join-Path $coreDir 'OdfPackage.Saving.Metadata.cs') -RegionName 'Saving - Metadata & Manifest' -BodyLines $metadataBody
Write-Host "  OdfPackage.Saving.Metadata.cs: $($metadataBody.Count) body lines"
Write-Host 'Done.'