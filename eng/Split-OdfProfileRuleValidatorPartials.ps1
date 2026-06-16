#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'
$sourcePath = Join-Path $complianceDir 'OdfProfileRuleValidator.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionStarts = @(96, 379, 593)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IEnumerable<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'IOException|Stream|File\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'Enumerable\.|\.Linq|\.Select\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'SecurityException') { [void]$needed.Add('System.Security') }
    if ($Text -match 'XmlReader|XmlConvert|XmlException') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'XElement|XAttribute|XDocument') { [void]$needed.Add('System.Xml.Linq') }
    if ($Text -match 'OdfPackage|OdfDocument|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.IO', 'System.Linq', 'System.Security',
        'System.Xml', 'System.Xml.Linq', 'OdfKit.Core'
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
    $out.Add('namespace OdfKit.Compliance;')
    $out.Add('')
    $out.Add('internal static partial class OdfProfileRuleValidator')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$header = $lines[0..12]
$fields = $lines[13..19]
$entryPoints = $lines[21..93]
$core = $header + $fields + $entryPoints
$core = $core | ForEach-Object {
    $_ -replace '^internal static class OdfProfileRuleValidator', 'internal static partial class OdfProfileRuleValidator'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfProfileRuleValidator.cs: $($core.Count) lines"

$files = @(
    @{ Start = 96; File = 'OdfProfileRuleValidator.SchemaPatterns.cs'; Region = 'Schema Pattern Validation' }
    @{ Start = 379; File = 'OdfProfileRuleValidator.Scanning.cs'; Region = 'XML Scanning & Macro Rules' }
    @{ Start = 593; File = 'OdfProfileRuleValidator.NamespaceRules.cs'; Region = 'Namespace & Version Rules' }
)
for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($i + 1 -lt $files.Count) { $files[$i + 1].Start - 2 } else { ($lines.Count - 2) }
    $block = $lines[$start..$end]
    Write-PartialFile -Path (Join-Path $complianceDir $files[$i].File) -RegionName $files[$i].Region -BodyLines $block
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

Write-Host 'Done.'